using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class ApprovalServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockedWorkflowExplainsSelfApprovalOrInactiveApproverAsync(bool isApproverInactive)
    {
        using (TestDbContextFactory factory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            await seedConfiguredApprovalWorkflowAsync(factory);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                ApprovalWorkflowStep step = db.ApprovalWorkflowSteps.OrderBy(item => item.StepOrder).First();
                step.ApproverSelectionType = EApproverSelectionType.SpecificUser;
                step.ApproverUserId = isApproverInactive ? 2 : 1;
                if (isApproverInactive)
                {
                    db.UserAccounts.Single(user => user.Id == 2).IsActive = false;
                }
                await db.SaveChangesAsync();
            }
            using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService drive = createApprovalDriveService();
                OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
                PermissionService folders = new PermissionService(factory, drive, cache, permissions);
                ApprovalService service = new ApprovalService(factory, folders, drive, permissions, new ApprovalPublicationService(factory, drive, permissions));
                UserSession session = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                ApprovalCreateContext context = await service.GetCreateContextAsync(session, CancellationToken.None);
                Assert.False(context.CanCreateRequest);
                Assert.Equal(EApprovalSetupArea.Workflow, context.SetupArea);
                Assert.Contains(isApproverInactive ? "활성 사용자" : "자신의 결재자", context.BlockReason);
            }
        }
    }

    [Fact]
    public async Task ConfiguredWorkflowBuildsApprovalLineFromRequesterTeamPositionAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            await seedConfiguredApprovalWorkflowAsync(dbContextFactory);
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory), new ApprovalPublicationService(dbContextFactory, driveService, new OrganizationPermissionService(dbContextFactory)));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "직책 기반 결재";
                formModel.SourceFolderId = "planning-materials-folder";
                formModel.DestinationFolderId = "planning-approved-folder";
                formModel.FileId = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserId = 2;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);

                Assert.True(createContext.CanCreateRequest);
                Assert.Equal(new int[] { 2 }, createContext.ApproverOptions.Select(option => option.UserId).ToArray());
                Assert.Equal(new int[] { 3 }, createContext.FinalApproverOptions.Select(option => option.UserId).ToArray());
                Assert.Equal(2, createContext.DefaultLineOptions.Single().StepCount.GetRequiredValue());
                Assert.Equal(2, createContext.DefaultLineOptions.Single().FirstApproverUserId);
                Assert.Equal(3, createContext.DefaultLineOptions.Single().FinalApproverUserId);
                Assert.Equal("워크플로 팀 결재자", createContext.ApproverOptions[0].PositionName);
                Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    ApprovalRequest approvalRequest = dbContext.ApprovalRequests.Include(request => request.Steps).Single();
                    Assert.Equal(2, approvalRequest.StepCount);
                    Assert.Equal(new int[] { 2, 3 }, approvalRequest.Steps.OrderBy(step => step.StepNumber).Select(step => step.ApproverUserId).ToArray());
                }
            }
        }
    }

    [Fact]
    public async Task ConfiguredWorkflowPrefersRequesterPositionConditionAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            await seedConfiguredApprovalWorkflowAsync(dbContextFactory);
            await seedRequesterPositionApprovalWorkflowAsync(dbContextFactory);
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory), new ApprovalPublicationService(dbContextFactory, driveService, new OrganizationPermissionService(dbContextFactory)));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "직책 조건 결재";
                formModel.SourceFolderId = "planning-materials-folder";
                formModel.DestinationFolderId = "planning-approved-folder";
                formModel.FileId = "planning-file";
                formModel.StepCount = 1;
                formModel.FinalApproverUserId = 3;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);

                Assert.True(createContext.CanCreateRequest);
                Assert.Empty(createContext.ApproverOptions);
                Assert.Equal(new int[] { 3 }, createContext.FinalApproverOptions.Select(option => option.UserId).ToArray());
                Assert.Equal(1, createContext.DefaultLineOptions.Single().StepCount.GetRequiredValue());
                Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    ApprovalRequest approvalRequest = dbContext.ApprovalRequests.Include(request => request.Steps).Single();
                    Assert.Equal(1, approvalRequest.StepCount);
                    Assert.Equal(new int[] { 3 }, approvalRequest.Steps.OrderBy(step => step.StepNumber).Select(step => step.ApproverUserId).ToArray());
                }
            }
        }
    }

    [Fact]
    public async Task ConfiguredWorkflowFirstApprovalAllowsUnchangedFinalApproverValueAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            await seedConfiguredApprovalWorkflowAsync(dbContextFactory);
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory), new ApprovalPublicationService(dbContextFactory, driveService, new OrganizationPermissionService(dbContextFactory)));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);
                UserSession finalApproverSession = createSession(3, "manager", "관리팀 매니저", EStoredCompatibilityRole.ManagementManager, null);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "직책 기반 결재";
                formModel.SourceFolderId = "planning-materials-folder";
                formModel.DestinationFolderId = "planning-approved-folder";
                formModel.FileId = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserId = 2;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> responsibleApprovals = await approvalService.GetPendingApprovalsAsync(responsibleSession, CancellationToken.None);
                int unchangedFinalApproverUserId = createContext.FinalApproverOptions.Single().UserId;
                ServiceResult firstApprovalResult = await approvalService.ApproveRequestAsync(responsibleSession, ApprovalRequestId.CreateExisting(responsibleApprovals.Single().Id), ApprovalUserId.CreateExisting(unchangedFinalApproverUserId), ApprovalComment.CreateOptional("확인"), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> finalApprovals = await approvalService.GetPendingApprovalsAsync(finalApproverSession, CancellationToken.None);

                Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
                Assert.True(firstApprovalResult.IsSuccess);
                Assert.Single(finalApprovals);
            }
        }
    }
}
