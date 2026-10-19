using System;
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
    public enum EApproverRestriction
    {
        InactiveAccount,
        InactivePosition,
        RevokedPermission
    }

    private const int HISTORY_MAXIMUM_COUNT = 100;

    [Theory]
    [InlineData(EApprovalAuditAction.Approved, 2, EApproverRestriction.InactiveAccount)]
    [InlineData(EApprovalAuditAction.Rejected, 2, EApproverRestriction.InactiveAccount)]
    [InlineData(EApprovalAuditAction.Approved, 3, EApproverRestriction.InactiveAccount)]
    [InlineData(EApprovalAuditAction.Rejected, 3, EApproverRestriction.InactiveAccount)]
    [InlineData(EApprovalAuditAction.Approved, 2, EApproverRestriction.InactivePosition)]
    [InlineData(EApprovalAuditAction.Rejected, 2, EApproverRestriction.InactivePosition)]
    [InlineData(EApprovalAuditAction.Approved, 3, EApproverRestriction.InactivePosition)]
    [InlineData(EApprovalAuditAction.Rejected, 3, EApproverRestriction.InactivePosition)]
    [InlineData(EApprovalAuditAction.Approved, 2, EApproverRestriction.RevokedPermission)]
    [InlineData(EApprovalAuditAction.Rejected, 2, EApproverRestriction.RevokedPermission)]
    [InlineData(EApprovalAuditAction.Approved, 3, EApproverRestriction.RevokedPermission)]
    [InlineData(EApprovalAuditAction.Rejected, 3, EApproverRestriction.RevokedPermission)]
    public async Task ApprovalDecisionRejectsStaleApproverSessionAsync(
        EApprovalAuditAction action,
        int approverUserId,
        EApproverRestriction restriction)
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            FakeDriveService driveService = createApprovalDriveService();
            OrganizationPermissionService organizationPermissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, organizationPermissionService);
            ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, organizationPermissionService);
            UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
            UserSession leaderSession = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);
            UserSession managerSession = createSession(3, "manager", "관리팀 매니저", EStoredCompatibilityRole.ManagementManager, 2);
            ApprovalRequestFormModel formModel = createRegressionApprovalForm();
            ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);
            Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
            IReadOnlyList<ApprovalRequestItem> pendingApprovals = await approvalService.GetPendingApprovalsAsync(leaderSession, CancellationToken.None);
            ApprovalRequestId requestId = ApprovalRequestId.CreateExisting(Assert.Single(pendingApprovals).Id);

            UserSession approverSession = leaderSession;
            int expectedStepNumber = 1;
            if (approverUserId == 3)
            {
                ServiceResult firstApprovalResult = await approvalService.ApproveRequestAsync(leaderSession, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("확인"), CancellationToken.None);
                Assert.True(firstApprovalResult.IsSuccess, firstApprovalResult.ErrorMessage);
                approverSession = managerSession;
                expectedStepNumber = 2;
            }

            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount approver = dbContext.UserAccounts.Include(user => user.Position).ThenInclude(position => position!.PermissionGrants).Single(user => user.Id == approverUserId);
                restrictApprover(approver, restriction);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            ServiceResult decisionResult = await executeRegressionDecisionAsync(approvalService, approverSession, requestId, action);

            Assert.False(decisionResult.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(decisionResult.ErrorMessage));
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                ApprovalRequest storedRequest = dbContext.ApprovalRequests.Include(request => request.Steps).Include(request => request.AuditLogs).Single();
                Assert.Equal(EApprovalRequestStatus.Pending, storedRequest.Status);
                Assert.Equal(expectedStepNumber, storedRequest.CurrentStepNumber);
                Assert.Equal(EApprovalStepStatus.Pending, storedRequest.Steps.Single(step => step.StepNumber == expectedStepNumber).Status);
                Assert.Equal(expectedStepNumber, storedRequest.AuditLogs.Count);
                Assert.Empty(storedRequest.ApprovedFileId);
                Assert.Empty(dbContext.UploadAuditLogs);
            }

            Assert.Equal(0, driveService.PublicationWriteCount);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task MissingRequiredWorkflowApproverBlocksFallbackSubmissionAsync(int inactiveApproverUserId)
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SecondFinalApprover))
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            await seedConfiguredApprovalWorkflowAsync(dbContextFactory);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.UserAccounts.Single(user => user.Id == inactiveApproverUserId).IsActive = false;
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            FakeDriveService driveService = createApprovalDriveService();
            OrganizationPermissionService organizationPermissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, organizationPermissionService);
            ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, organizationPermissionService);
            UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
            ApprovalRequestFormModel formModel = createRegressionApprovalForm();
            formModel.StepCount = 1;
            formModel.FirstApproverUserId = 0;
            formModel.FinalApproverUserId = 7;

            ApprovalCreateContext context = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
            ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);

            Assert.False(context.CanCreateRequest);
            Assert.Contains("필수", context.BlockReason);
            Assert.False(submitResult.IsSuccess);
            Assert.Equal(context.BlockReason, submitResult.ErrorMessage);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Assert.Empty(dbContext.ApprovalRequests);
                Assert.Empty(dbContext.ApprovalAuditLogs);
            }
        }
    }

    [Fact]
    public async Task MissingOptionalIntermediateApproverStillAllowsConfiguredFinalApprovalAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            await seedConfiguredApprovalWorkflowAsync(dbContextFactory);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.UserAccounts.Single(user => user.Id == 2).IsActive = false;
                dbContext.ApprovalWorkflowSteps.Single(step => step.StepOrder == 1).IsRequired = false;
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            FakeDriveService driveService = createApprovalDriveService();
            OrganizationPermissionService organizationPermissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, organizationPermissionService);
            ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, organizationPermissionService);
            UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
            ApprovalRequestFormModel formModel = createRegressionApprovalForm();
            formModel.StepCount = 1;
            formModel.FirstApproverUserId = 0;
            formModel.FinalApproverUserId = 3;

            ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);

            Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                ApprovalStep step = Assert.Single(dbContext.ApprovalSteps);
                Assert.Equal(3, step.ApproverUserId);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task RelatedHistoryFiltersAccessBeforeApplyingLimitAsync(int viewerUserId)
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                DateTime requestedAt = DateTime.UtcNow;
                ApprovalRequest relatedRequest = createRegressionHistoryRequest(1, 1, requestedAt.AddDays(-1));
                relatedRequest.Title = "조회 가능한 이전 결재";
                ApprovalStep rejectedStep = new ApprovalStep();
                rejectedStep.StepNumber = 1;
                rejectedStep.ApproverUserId = 2;
                rejectedStep.Status = EApprovalStepStatus.Rejected;
                relatedRequest.Steps.Add(rejectedStep);
                dbContext.ApprovalRequests.Add(relatedRequest);
                for (int requestIndex = 0; requestIndex < HISTORY_MAXIMUM_COUNT; requestIndex++)
                {
                    dbContext.ApprovalRequests.Add(createRegressionHistoryRequest(6, 2, requestedAt.AddMinutes(-requestIndex)));
                }

                UserAccount historyReader = dbContext.UserAccounts.Include(user => user.Position).Single(user => user.Id == 4);
                PositionPermissionGrant historyGrant = new PositionPermissionGrant();
                historyGrant.PositionId = historyReader.PositionId.GetValueOrDefault();
                historyGrant.PermissionKey = PermissionKeys.APPROVAL_HISTORY_READ;
                historyGrant.IsAllowed = true;
                historyGrant.ScopeType = EPermissionScopeType.SelectedTeam;
                historyGrant.TargetTeamId = 1;
                dbContext.PositionPermissionGrants.Add(historyGrant);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            FakeDriveService driveService = createApprovalDriveService();
            OrganizationPermissionService organizationPermissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, organizationPermissionService);
            ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, organizationPermissionService);
            UserSession viewerSession = createSession(viewerUserId, "viewer", "조회자", EStoredCompatibilityRole.TeamUser, 1);
            UserSession unrelatedSession = createSession(5, "unrelated", "무관한 사용자", EStoredCompatibilityRole.ManagementDeputyDirector, 2);

            IReadOnlyList<ApprovalRequestItem> history = await approvalService.GetRelatedHistoryAsync(viewerSession, CancellationToken.None);
            IReadOnlyList<ApprovalRequestItem> unrelatedHistory = await approvalService.GetRelatedHistoryAsync(unrelatedSession, CancellationToken.None);

            Assert.Equal("조회 가능한 이전 결재", Assert.Single(history).Title);
            Assert.Empty(unrelatedHistory);
        }
    }

    [Fact]
    public async Task RelatedHistoryKeepsOwnPendingRequestsOutsideCompletedHistoryLimitAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            List<int> expectedRequestIds = new List<int>();
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                DateTime requestedAt = DateTime.UtcNow;
                List<ApprovalRequest> completedRequests = new List<ApprovalRequest>();
                for (int requestIndex = 0; requestIndex <= HISTORY_MAXIMUM_COUNT; requestIndex++)
                {
                    ApprovalRequest request = createRegressionHistoryRequest(1, 1, requestedAt);
                    completedRequests.Add(request);
                    dbContext.ApprovalRequests.Add(request);
                }

                ApprovalRequest pendingRequest = createRegressionHistoryRequest(1, 1, requestedAt.AddYears(-1));
                pendingRequest.Status = EApprovalRequestStatus.Pending;
                dbContext.ApprovalRequests.Add(pendingRequest);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                expectedRequestIds.Add(pendingRequest.Id);
                expectedRequestIds.AddRange(completedRequests.OrderByDescending(request => request.Id).Take(HISTORY_MAXIMUM_COUNT).Select(request => request.Id));
            }

            FakeDriveService driveService = createApprovalDriveService();
            OrganizationPermissionService organizationPermissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, organizationPermissionService);
            ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, organizationPermissionService);
            UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);

            IReadOnlyList<ApprovalRequestItem> history = await approvalService.GetRelatedHistoryAsync(requesterSession, CancellationToken.None);

            Assert.Equal(expectedRequestIds, history.Select(request => request.Id).ToList());
            Assert.Equal(EApprovalRequestStatus.Pending, history[0].Status);
            Assert.All(history.Skip(1), request => Assert.Equal(EApprovalRequestStatus.Rejected, request.Status));
        }
    }

    [Fact]
    public async Task GlobalHistoryPermissionIncludesRequestsWithoutTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                ApprovalRequest request = createRegressionHistoryRequest(1, 1, DateTime.UtcNow);
                request.TeamId = null;
                dbContext.ApprovalRequests.Add(request);
                UserAccount reader = dbContext.UserAccounts.Include(user => user.Position).Single(user => user.Id == 4);
                Assert.NotNull(reader.Position);
                addPermissionGrant(reader.Position, PermissionKeys.APPROVAL_HISTORY_READ, EPermissionScopeType.Global);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            FakeDriveService driveService = createApprovalDriveService();
            OrganizationPermissionService organizationPermissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, organizationPermissionService);
            ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, organizationPermissionService);
            UserSession readerSession = createSession(4, "director", "디렉터", EStoredCompatibilityRole.ManagementDirector, 2);

            IReadOnlyList<ApprovalRequestItem> history = await approvalService.GetRelatedHistoryAsync(readerSession, CancellationToken.None);

            Assert.Single(history);
        }
    }

    private static Task<ServiceResult> executeRegressionDecisionAsync(
        ApprovalService approvalService,
        UserSession approverSession,
        ApprovalRequestId requestId,
        EApprovalAuditAction action)
    {
        if (action == EApprovalAuditAction.Approved)
        {
            return approvalService.ApproveRequestAsync(approverSession, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("승인"), CancellationToken.None);
        }

        return approvalService.RejectRequestAsync(approverSession, requestId, ApprovalComment.CreateOptional("반려"), CancellationToken.None);
    }

    private static ApprovalRequestFormModel createRegressionApprovalForm()
    {
        ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
        formModel.Title = "회귀 테스트 결재";
        formModel.SourceFolderId = "planning-materials-folder";
        formModel.DestinationFolderId = "planning-approved-folder";
        formModel.FileId = "planning-file";
        formModel.StepCount = 2;
        formModel.FirstApproverUserId = 2;
        return formModel;
    }

    private static ApprovalRequest createRegressionHistoryRequest(int requesterUserId, int teamId, DateTime requestedAt)
    {
        ApprovalRequest request = new ApprovalRequest();
        request.Title = "결재 기록 회귀 테스트";
        request.RequesterUserId = requesterUserId;
        request.TeamId = teamId;
        request.RequestedAt = requestedAt;
        request.Status = EApprovalRequestStatus.Rejected;
        request.StepCount = 1;
        request.CurrentStepNumber = 1;
        return request;
    }

    private static void restrictApprover(UserAccount approver, EApproverRestriction restriction)
    {
        Assert.NotNull(approver.Position);
        switch (restriction)
        {
            case EApproverRestriction.InactiveAccount:
                approver.IsActive = false;
                break;
            case EApproverRestriction.InactivePosition:
                approver.Position.IsActive = false;
                break;
            case EApproverRestriction.RevokedPermission:
                foreach (PositionPermissionGrant grant in approver.Position.PermissionGrants)
                {
                    grant.IsAllowed = false;
                }

                break;
            default:
                System.Diagnostics.Debug.Fail("Unexpected approver restriction: " + restriction);
                throw new ArgumentOutOfRangeException(nameof(restriction));
        }
    }
}
