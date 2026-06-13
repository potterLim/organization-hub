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

public sealed class ApprovalServiceTests
{
    [Fact]
    public async Task TwoStepApprovalUsesSingleFinalApproverAndPublishesApprovedFileAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EUserRole.TeamLeader, 1);
                UserSession managerSession = createSession(3, "manager", "관리팀 매니저", EUserRole.ManagementManager, null);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "부스 운영안";
                formModel.SourceFolderID = "planning-materials-folder";
                formModel.DestinationFolderID = "planning-approved-folder";
                formModel.FileID = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserID = 2;

                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> leaderApprovals = await approvalService.GetPendingApprovalsAsync(responsibleSession, CancellationToken.None);
                ServiceResult firstApprovalResult = await approvalService.ApproveRequestAsync(responsibleSession, leaderApprovals[0].ID, 0, "확인", CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> finalApproverApprovals = await approvalService.GetPendingApprovalsAsync(managerSession, CancellationToken.None);
                ServiceResult finalApprovalResult = await approvalService.ApproveRequestAsync(managerSession, finalApproverApprovals[0].ID, 0, "최종 확인", CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> approvedRequests = await approvalService.GetApprovedRequestsAsync(requesterSession, CancellationToken.None);
                UploadAuditLog approvedUploadAuditLog;
                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    approvedUploadAuditLog = dbContext.UploadAuditLogs.Single(auditLog => auditLog.FileID == "copied-planning-file");
                }

                Assert.True(createContext.CanCreateRequest);
                Assert.Single(createContext.FinalApproverCandidates);
                Assert.True(submitResult.IsSuccess);
                Assert.Single(leaderApprovals);
                Assert.True(firstApprovalResult.IsSuccess);
                Assert.Single(finalApproverApprovals);
                Assert.True(finalApprovalResult.IsSuccess);
                Assert.Single(approvedRequests);
                Assert.Equal("copied-planning-file", approvedRequests[0].ApprovedFileID);
                Assert.Equal("기획팀 결재 완료 자료", approvedRequests[0].ApprovedFolderDisplayName);
                Assert.Equal("기획팀원", approvedUploadAuditLog.UploaderDisplayName);
            }
        }
    }

    [Fact]
    public async Task ConfiguredWorkflowBuildsApprovalLineFromRequesterTeamPositionAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            int leaderPositionID = await seedConfiguredApprovalWorkflowAsync(dbContextFactory);
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "직책 기반 결재";
                formModel.SourceFolderID = "planning-materials-folder";
                formModel.DestinationFolderID = "planning-approved-folder";
                formModel.FileID = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserID = 2;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel, CancellationToken.None);

                Assert.True(createContext.CanCreateRequest);
                Assert.Equal(new[] { 2 }, createContext.ApproverOptions.Select(option => option.UserID).ToArray());
                Assert.Equal(new[] { 3 }, createContext.FinalApproverOptions.Select(option => option.UserID).ToArray());
                Assert.Equal(2, createContext.DefaultLineOptions.Single().StepCount);
                Assert.Equal(2, createContext.DefaultLineOptions.Single().FirstApproverUserID);
                Assert.Equal(3, createContext.DefaultLineOptions.Single().FinalApproverUserID);
                Assert.Equal("팀 결재자", createContext.ApproverOptions[0].RoleName);
                Assert.True(submitResult.IsSuccess);
                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    ApprovalRequest approvalRequest = dbContext.ApprovalRequests.Include(request => request.Steps).Single();
                    Assert.Equal(2, approvalRequest.StepCount);
                    Assert.Equal(new[] { 2, 3 }, approvalRequest.Steps.OrderBy(step => step.StepNumber).Select(step => step.ApproverUserID).ToArray());
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EUserRole.TeamLeader, 1);
                UserSession finalApproverSession = createSession(3, "manager", "관리팀 매니저", EUserRole.ManagementManager, null);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "직책 기반 결재";
                formModel.SourceFolderID = "planning-materials-folder";
                formModel.DestinationFolderID = "planning-approved-folder";
                formModel.FileID = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserID = 2;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> responsibleApprovals = await approvalService.GetPendingApprovalsAsync(responsibleSession, CancellationToken.None);
                int unchangedFinalApproverUserID = createContext.FinalApproverOptions.Single().UserID;
                ServiceResult firstApprovalResult = await approvalService.ApproveRequestAsync(responsibleSession, responsibleApprovals.Single().ID, unchangedFinalApproverUserID, "확인", CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> finalApprovals = await approvalService.GetPendingApprovalsAsync(finalApproverSession, CancellationToken.None);

                Assert.True(submitResult.IsSuccess);
                Assert.True(firstApprovalResult.IsSuccess);
                Assert.Single(finalApprovals);
            }
        }
    }

    [Fact]
    public async Task ApprovedRequestsAreFilteredByApprovedDestinationPermissionAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                driveService.AddFolder("outside-approved-folder", "다른 결재 완료 자료", "official-root");
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);
                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    ApprovalRequest approvalRequest = new ApprovalRequest();
                    approvalRequest.Title = "권한 밖 승인본";
                    approvalRequest.FileID = "planning-file";
                    approvalRequest.FileName = "2026-05-23_기획팀_부스운영안.pdf";
                    approvalRequest.MimeType = "application/pdf";
                    approvalRequest.SourceFolderID = "planning-materials-folder";
                    approvalRequest.SourceFolderKind = EDriveFolderKind.TeamMaterials;
                    approvalRequest.TeamID = 1;
                    approvalRequest.TeamName = "기획팀";
                    approvalRequest.RequesterUserID = 1;
                    approvalRequest.RequesterDisplayName = "기획팀원";
                    approvalRequest.RequestedAt = System.DateTime.UtcNow.AddMinutes(-10);
                    approvalRequest.Status = EApprovalRequestStatus.Approved;
                    approvalRequest.StepCount = 1;
                    approvalRequest.CurrentStepNumber = 1;
                    approvalRequest.ApprovedFileID = "planning-file";
                    approvalRequest.ApprovedFolderID = "outside-approved-folder";
                    approvalRequest.ApprovedFolderDisplayName = "다른 결재 완료 자료";
                    approvalRequest.CompletedAt = System.DateTime.UtcNow;
                    dbContext.ApprovalRequests.Add(approvalRequest);
                    dbContext.SaveChanges();
                }

                IReadOnlyList<ApprovalRequestItem> approvedRequests = await approvalService.GetApprovedRequestsAsync(requesterSession, CancellationToken.None);

                Assert.Empty(approvedRequests);
            }
        }
    }

    [Fact]
    public async Task MultipleFinalApproverCandidatesRequireRequesterChoiceAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SecondFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);
                ApprovalRequestFormModel missingFinalApproverFormModel = new ApprovalRequestFormModel();
                missingFinalApproverFormModel.Title = "부스 운영안";
                missingFinalApproverFormModel.SourceFolderID = "planning-materials-folder";
                missingFinalApproverFormModel.DestinationFolderID = "planning-approved-folder";
                missingFinalApproverFormModel.FileID = "planning-file";
                missingFinalApproverFormModel.StepCount = 2;
                missingFinalApproverFormModel.FirstApproverUserID = 2;
                ApprovalRequestFormModel selectedFinalApproverFormModel = new ApprovalRequestFormModel();
                selectedFinalApproverFormModel.Title = "부스 운영안";
                selectedFinalApproverFormModel.SourceFolderID = "planning-materials-folder";
                selectedFinalApproverFormModel.DestinationFolderID = "planning-approved-folder";
                selectedFinalApproverFormModel.FileID = "planning-file";
                selectedFinalApproverFormModel.StepCount = 2;
                selectedFinalApproverFormModel.FirstApproverUserID = 2;
                selectedFinalApproverFormModel.FinalApproverUserID = 7;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ServiceResult missingFinalApproverResult = await approvalService.SubmitRequestAsync(requesterSession, missingFinalApproverFormModel, CancellationToken.None);
                ServiceResult selectedFinalApproverResult = await approvalService.SubmitRequestAsync(requesterSession, selectedFinalApproverFormModel, CancellationToken.None);

                Assert.Equal(2, createContext.FinalApproverCandidates.Count);
                Assert.False(missingFinalApproverResult.IsSuccess);
                Assert.True(selectedFinalApproverResult.IsSuccess);
            }
        }
    }

    [Fact]
    public async Task RejectedApprovalHistoryIsVisibleToRequesterAndRejecterAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EUserRole.TeamLeader, 1);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "부스 운영안";
                formModel.SourceFolderID = "planning-materials-folder";
                formModel.DestinationFolderID = "planning-approved-folder";
                formModel.FileID = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserID = 2;

                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> leaderApprovals = await approvalService.GetPendingApprovalsAsync(responsibleSession, CancellationToken.None);
                ServiceResult rejectResult = await approvalService.RejectRequestAsync(responsibleSession, leaderApprovals[0].ID, "보완 필요", CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> requesterHistory = await approvalService.GetRelatedHistoryAsync(requesterSession, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> rejecterHistory = await approvalService.GetRelatedHistoryAsync(responsibleSession, CancellationToken.None);

                Assert.True(submitResult.IsSuccess);
                Assert.True(rejectResult.IsSuccess);
                Assert.Single(requesterHistory);
                Assert.Single(rejecterHistory);
                Assert.Equal(EApprovalRequestStatus.Rejected, requesterHistory[0].Status);
            }
        }
    }

    [Fact]
    public async Task TeamLeaderCanSubmitOneStepApprovalToManagerAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EUserRole.TeamLeader, 1);
                UserSession managerSession = createSession(3, "manager", "관리팀 매니저", EUserRole.ManagementManager, null);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "책임자 단독 상신";
                formModel.SourceFolderID = "planning-materials-folder";
                formModel.DestinationFolderID = "planning-approved-folder";
                formModel.FileID = "planning-file";
                formModel.StepCount = 1;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(responsibleSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(responsibleSession, formModel, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> finalApproverApprovals = await approvalService.GetPendingApprovalsAsync(managerSession, CancellationToken.None);

                Assert.Contains(1, createContext.AvailableStepCounts);
                Assert.True(createContext.UsesDefaultFinalApprover);
                Assert.True(submitResult.IsSuccess);
                Assert.Single(finalApproverApprovals);
            }
        }
    }

    [Fact]
    public async Task ManagementRequesterUsesFlexibleApprovalCandidatesInRoleOrderAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SecondFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession managementSession = createSession(3, "manager", "관리팀 매니저", EUserRole.ManagementManager, null);
                UserSession directorSession = createSession(4, "director", "디렉터", EUserRole.ManagementDirector, null);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "관리팀 자율 결재";
                formModel.SourceFolderID = "planning-materials-folder";
                formModel.DestinationFolderID = "planning-approved-folder";
                formModel.FileID = "planning-file";
                formModel.StepCount = 1;
                formModel.FinalApproverUserID = 4;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(managementSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(managementSession, formModel, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> directorApprovals = await approvalService.GetPendingApprovalsAsync(directorSession, CancellationToken.None);

                Assert.Contains(1, createContext.AvailableStepCounts);
                Assert.False(createContext.UsesDefaultFinalApprover);
                Assert.Equal(new[] { 4, 5, 7, 2, 6, 9 }, createContext.ApproverOptions.Select(option => option.UserID).ToArray());
                Assert.Equal(new[] { 4, 5, 7, 2, 6, 9 }, createContext.FinalApproverOptions.Select(option => option.UserID).ToArray());
                Assert.DoesNotContain(createContext.FinalApproverOptions, option => option.UserID == 3);
                Assert.DoesNotContain(createContext.FinalApproverOptions, option => option.UserID == 8);
                Assert.True(submitResult.IsSuccess);
                Assert.Single(directorApprovals);
            }
        }
    }

    [Fact]
    public async Task DirectorRequesterCanSelectNonManagerFinalApproverAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession directorSession = createSession(4, "director", "디렉터", EUserRole.ManagementDirector, null);
                UserSession deputyDirectorSession = createSession(5, "deputy.director", "부디렉터", EUserRole.ManagementDeputyDirector, null);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "관리팀 자유 결재";
                formModel.SourceFolderID = "planning-materials-folder";
                formModel.DestinationFolderID = "planning-approved-folder";
                formModel.FileID = "planning-file";
                formModel.StepCount = 1;
                formModel.FinalApproverUserID = 5;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(directorSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(directorSession, formModel, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> deputyApprovals = await approvalService.GetPendingApprovalsAsync(deputyDirectorSession, CancellationToken.None);

                Assert.False(createContext.UsesDefaultFinalApprover);
                Assert.Equal(new[] { 5, 3, 2, 6, 9 }, createContext.FinalApproverOptions.Select(option => option.UserID).ToArray());
                Assert.True(submitResult.IsSuccess);
                Assert.Single(deputyApprovals);
            }
        }
    }

    [Theory]
    [InlineData(4, EUserRole.ManagementDirector, 2, 3, 0, 5)]
    [InlineData(4, EUserRole.ManagementDirector, 3, 6, 3, 5)]
    [InlineData(5, EUserRole.ManagementDeputyDirector, 3, 6, 3, 4)]
    [InlineData(3, EUserRole.ManagementManager, 3, 6, 5, 4)]
    [InlineData(6, EUserRole.ManagementStaff, 3, 9, 3, 4)]
    [InlineData(9, EUserRole.ManagementRecorder, 3, 6, 3, 4)]
    public async Task ManagementDefaultLinesFollowOfficePolicyAsync(int requesterUserID, EUserRole requesterRole, int stepCount, int expectedFirstApproverUserID, int expectedSecondApproverUserID, int expectedFinalApproverUserID)
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(requesterUserID, "requester", "요청자", requesterRole, null);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ApprovalDefaultLineOption defaultLine = createContext.DefaultLineOptions.Single(defaultLine => defaultLine.StepCount == stepCount);

                Assert.Equal(expectedFirstApproverUserID, defaultLine.FirstApproverUserID);
                Assert.Equal(expectedSecondApproverUserID, defaultLine.SecondApproverUserID);
                Assert.Equal(expectedFinalApproverUserID, defaultLine.FinalApproverUserID);
            }
        }
    }

    [Fact]
    public async Task TeamRequesterDefaultLinesUseTeamLeaderAndOfficeRolesAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession teamMemberSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);
                UserSession teamLeaderSession = createSession(2, "planning.lead", "기획 책임자", EUserRole.TeamLeader, 1);

                ApprovalCreateContext memberCreateContext = await approvalService.GetCreateContextAsync(teamMemberSession, CancellationToken.None);
                ApprovalCreateContext leaderCreateContext = await approvalService.GetCreateContextAsync(teamLeaderSession, CancellationToken.None);
                ApprovalDefaultLineOption memberDefaultLine = memberCreateContext.DefaultLineOptions.Single(defaultLine => defaultLine.StepCount == 3);
                ApprovalDefaultLineOption leaderDefaultLine = leaderCreateContext.DefaultLineOptions.Single(defaultLine => defaultLine.StepCount == 3);

                Assert.Equal(2, memberDefaultLine.FirstApproverUserID);
                Assert.Equal(6, memberDefaultLine.SecondApproverUserID);
                Assert.Equal(3, memberDefaultLine.FinalApproverUserID);
                Assert.Equal(9, leaderDefaultLine.FirstApproverUserID);
                Assert.Equal(6, leaderDefaultLine.SecondApproverUserID);
                Assert.Equal(3, leaderDefaultLine.FinalApproverUserID);
            }
        }
    }

    [Fact]
    public void DefaultLinePolicyChoosesFinalApproverThatCanCompleteSelectedStep()
    {
        UserSession directorSession = createSession(4, "director", "디렉터", EUserRole.ManagementDirector, null);
        List<ApprovalUserOption> approverOptions = new List<ApprovalUserOption>
        {
            createApprovalUserOption(5, "부디렉터", EUserRole.ManagementDeputyDirector, null),
            createApprovalUserOption(6, "관리팀 담당자", EUserRole.ManagementStaff, null)
        };
        List<ApprovalUserOption> finalApproverOptions = new List<ApprovalUserOption>
        {
            createApprovalUserOption(5, "부디렉터", EUserRole.ManagementDeputyDirector, null),
            createApprovalUserOption(3, "매니저", EUserRole.ManagementManager, null)
        };

        IReadOnlyList<ApprovalDefaultLineOption> defaultLines = ApprovalDefaultLinePolicy.CreateDefaultLineOptions(directorSession, approverOptions, finalApproverOptions, new[] { 3 });
        ApprovalDefaultLineOption defaultLine = defaultLines.Single();

        Assert.Equal(6, defaultLine.FirstApproverUserID);
        Assert.Equal(5, defaultLine.SecondApproverUserID);
        Assert.Equal(3, defaultLine.FinalApproverUserID);
    }

    [Fact]
    public async Task NormalRequesterApprovalOptionsHideAdminDirectorDeputyAndManagerAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);

                Assert.Contains(createContext.ApproverOptions, option => option.UserID == 2);
                Assert.Contains(createContext.ApproverOptions, option => option.UserID == 6);
                Assert.DoesNotContain(createContext.ApproverOptions, option => option.UserID == 3);
                Assert.DoesNotContain(createContext.ApproverOptions, option => option.UserID == 4);
                Assert.DoesNotContain(createContext.ApproverOptions, option => option.UserID == 5);
                Assert.DoesNotContain(createContext.ApproverOptions, option => option.UserID == 8);
                Assert.Single(createContext.FinalApproverOptions);
                Assert.Equal(3, createContext.FinalApproverOptions[0].UserID);
            }
        }
    }

    [Fact]
    public async Task AdminCannotCreateApprovalRequestAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService);
                UserSession adminSession = createSession(8, "admin", "사이트 관리자", EUserRole.Admin, null);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(adminSession, CancellationToken.None);

                Assert.False(createContext.CanCreateRequest);
                Assert.Empty(createContext.ApproverOptions);
                Assert.Empty(createContext.FinalApproverOptions);
            }
        }
    }

    private static async Task<int> seedConfiguredApprovalWorkflowAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition requesterPosition = new OrganizationPosition();
            requesterPosition.TeamID = 1;
            requesterPosition.Name = "요청자";
            requesterPosition.Description = string.Empty;
            requesterPosition.SortOrder = 10;
            requesterPosition.IsActive = true;
            addPermissionGrant(requesterPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
            addPermissionGrant(requesterPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.OwnTeam);

            OrganizationPosition leaderPosition = new OrganizationPosition();
            leaderPosition.TeamID = 1;
            leaderPosition.Name = "팀 결재자";
            leaderPosition.Description = string.Empty;
            leaderPosition.SortOrder = 20;
            leaderPosition.IsActive = true;

            UserAccount requester = dbContext.UserAccounts.Single(user => user.ID == 1);
            UserAccount leader = dbContext.UserAccounts.Single(user => user.ID == 2);
            requester.Position = requesterPosition;
            leader.Position = leaderPosition;

            ApprovalWorkflow workflow = new ApprovalWorkflow();
            workflow.Name = "팀 자료 결재";
            workflow.Description = string.Empty;
            workflow.AppliesToTeamID = 1;
            workflow.Priority = 0;
            workflow.IsActive = true;
            ApprovalWorkflowStep firstStep = new ApprovalWorkflowStep();
            firstStep.StepOrder = 1;
            firstStep.StepName = "팀 확인";
            firstStep.ApproverSelectionType = EApproverSelectionType.PositionInRequesterTeam;
            firstStep.ApproverPosition = leaderPosition;
            firstStep.RequiredPermissionKey = PermissionKeys.APPROVAL_APPROVE;
            firstStep.ScopeType = EPermissionScopeType.OwnTeam;
            firstStep.IsRequired = true;
            ApprovalWorkflowStep finalStep = new ApprovalWorkflowStep();
            finalStep.StepOrder = 2;
            finalStep.StepName = "최종 승인";
            finalStep.ApproverSelectionType = EApproverSelectionType.SpecificUser;
            finalStep.ApproverUserID = 3;
            finalStep.RequiredPermissionKey = PermissionKeys.APPROVAL_FINAL_APPROVE;
            finalStep.ScopeType = EPermissionScopeType.Global;
            finalStep.IsRequired = true;
            workflow.Steps.Add(firstStep);
            workflow.Steps.Add(finalStep);
            dbContext.Positions.Add(requesterPosition);
            dbContext.Positions.Add(leaderPosition);
            dbContext.ApprovalWorkflows.Add(workflow);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return leaderPosition.ID;
        }
    }

    private static void addPermissionGrant(OrganizationPosition position, string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
        permissionGrant.Position = position;
        permissionGrant.PermissionKey = permissionKey;
        permissionGrant.ScopeType = scopeType;
        permissionGrant.IsAllowed = true;
        position.PermissionGrants.Add(permissionGrant);
    }

    private static TestDbContextFactory createApprovalDbContextFactory(EFinalApproverSeedMode finalApproverSeedMode)
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.ID = 1;
            setting.OfficialRootFolderID = "official-root";
            Team planningTeam = new Team();
            planningTeam.ID = 1;
            planningTeam.TeamKey = "Planning";
            planningTeam.DisplayName = "기획팀";
            planningTeam.IsActive = true;
            planningTeam.SortOrder = 10;
            planningTeam.MaterialsFolderID = "planning-materials-folder";
            planningTeam.ApprovedFolderID = "planning-approved-folder";
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(planningTeam);
            dbContext.UserAccounts.Add(createUser(1, "planning.member", "기획팀원", EUserRole.TeamUser, 1));
            dbContext.UserAccounts.Add(createUser(2, "planning.lead", "기획 책임자", EUserRole.TeamLeader, 1));
            dbContext.UserAccounts.Add(createUser(3, "manager", "관리팀 매니저", EUserRole.ManagementManager, null));
            dbContext.UserAccounts.Add(createUser(4, "director", "디렉터", EUserRole.ManagementDirector, null));
            dbContext.UserAccounts.Add(createUser(5, "deputy.director", "부디렉터", EUserRole.ManagementDeputyDirector, null));
            dbContext.UserAccounts.Add(createUser(6, "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, null));
            dbContext.UserAccounts.Add(createUser(9, "management.recorder", "관리팀 기록자", EUserRole.ManagementRecorder, null));
            if (finalApproverSeedMode == EFinalApproverSeedMode.SecondFinalApprover)
            {
                dbContext.UserAccounts.Add(createUser(7, "manager2", "매니저 담당", EUserRole.ManagementManager, null));
            }

            dbContext.UserAccounts.Add(createUser(8, "admin", "사이트 관리자", EUserRole.Admin, null));

            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static ApprovalUserOption createApprovalUserOption(int id, string displayName, EUserRole role, int? teamID)
    {
        ApprovalUserOption option = new ApprovalUserOption();
        option.UserID = id;
        option.DisplayName = displayName;
        option.Role = role;
        option.TeamID = teamID;
        return option;
    }

    private static UserAccount createUser(int id, string username, string displayName, EUserRole role, int? teamID)
    {
        UserAccount userAccount = new UserAccount();
        userAccount.ID = id;
        userAccount.Username = username;
        userAccount.DisplayName = displayName;
        userAccount.PasswordHash = "hash";
        userAccount.Role = role;
        userAccount.TeamID = teamID;
        userAccount.IsActive = true;
        userAccount.CreatedAt = System.DateTime.UtcNow;
        return userAccount;
    }

    private static FakeDriveService createApprovalDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("planning-team-folder", "기획팀", "official-root");
        driveService.AddFolder("planning-materials-folder", "기획팀 자료", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-approved-folder", "기획팀 결재 완료 자료", "planning-team-folder", "official-root");
        driveService.AddFile("planning-file", "2026-05-23_기획팀_부스운영안.pdf", "application/pdf", "planning-materials-folder", "planning-team-folder", "official-root");
        return driveService;
    }

    private static UserSession createSession(int userID, string username, string displayName, EUserRole role, int? teamID)
    {
        if (teamID.HasValue)
        {
            return new UserSession(userID, username, displayName, role, teamID, "Planning", "기획팀");
        }

        return new UserSession(userID, username, displayName, role, null, string.Empty, string.Empty);
    }

    private enum EFinalApproverSeedMode
    {
        SingleFinalApprover,
        SecondFinalApprover
    }
}
