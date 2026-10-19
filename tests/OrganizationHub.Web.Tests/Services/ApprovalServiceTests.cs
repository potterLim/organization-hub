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
    [Fact]
    public async Task TwoStepApprovalUsesSingleFinalApproverAndPublishesApprovedFileAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);
                UserSession managerSession = createSession(3, "manager", "관리팀 매니저", EStoredCompatibilityRole.ManagementManager, null);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "부스 운영안";
                formModel.SourceFolderId = "planning-materials-folder";
                formModel.DestinationFolderId = "planning-approved-folder";
                formModel.FileId = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserId = 2;

                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> leaderApprovals = await approvalService.GetPendingApprovalsAsync(responsibleSession, CancellationToken.None);
                ServiceResult firstApprovalResult = await approvalService.ApproveRequestAsync(responsibleSession, ApprovalRequestId.CreateExisting(leaderApprovals[0].Id), ApprovalUserId.Empty, ApprovalComment.CreateOptional("확인"), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> finalApproverApprovals = await approvalService.GetPendingApprovalsAsync(managerSession, CancellationToken.None);
                ServiceResult finalApprovalResult = await approvalService.ApproveRequestAsync(managerSession, ApprovalRequestId.CreateExisting(finalApproverApprovals[0].Id), ApprovalUserId.Empty, ApprovalComment.CreateOptional("최종 확인"), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> approvedRequests = await approvalService.GetApprovedRequestsAsync(requesterSession, CancellationToken.None);
                UploadAuditLog approvedUploadAuditLog;
                ApprovalRequest storedRequest;
                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    approvedUploadAuditLog = dbContext.UploadAuditLogs.Single();
                    storedRequest = dbContext.ApprovalRequests
                        .Include(approvalRequest => approvalRequest.Steps)
                        .Include(approvalRequest => approvalRequest.AuditLogs)
                        .Single();
                }

                Assert.True(createContext.CanCreateRequest);
                Assert.Single(createContext.FinalApproverCandidates);
                Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
                Assert.Single(leaderApprovals);
                Assert.True(firstApprovalResult.IsSuccess);
                Assert.Single(finalApproverApprovals);
                Assert.True(finalApprovalResult.IsSuccess);
                Assert.Single(approvedRequests);
                Assert.Equal(approvedUploadAuditLog.FileId, approvedRequests[0].ApprovedFileId);
                Assert.False(string.IsNullOrWhiteSpace(approvedRequests[0].ApprovedFileId));
                Assert.Equal("기획팀 결재 완료 자료", approvedRequests[0].ApprovedFolderDisplayName);
                Assert.Equal("기획팀원", approvedUploadAuditLog.UploaderDisplayName);
                Assert.Equal("기획팀", approvedUploadAuditLog.UploaderTeamName);
                Assert.Equal("요청자", approvedUploadAuditLog.UploaderPositionName);
                Assert.Equal("기획팀", storedRequest.RequesterTeamName);
                Assert.Equal("요청자", storedRequest.RequesterPositionName);
                Assert.Contains(storedRequest.Steps, step => step.ApproverTeamName == "기획팀" && step.ApproverPositionName == "팀 결재자");
                Assert.Contains(storedRequest.Steps, step => step.ApproverTeamName == "관리팀" && step.ApproverPositionName == "최종 결재자");
                Assert.Contains(storedRequest.AuditLogs, auditLog => auditLog.ActorTeamName == "기획팀" && auditLog.ActorPositionName == "팀 결재자");
                Assert.Contains(storedRequest.AuditLogs, auditLog => auditLog.Action == EApprovalAuditAction.Approved && auditLog.ActorTeamName == "관리팀" && auditLog.ActorPositionName == "최종 결재자");
                Assert.DoesNotContain(storedRequest.AuditLogs, auditLog => auditLog.Action == EApprovalAuditAction.Completed);
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    ApprovalRequest approvalRequest = new ApprovalRequest();
                    approvalRequest.Title = "권한 밖 승인본";
                    approvalRequest.FileId = "planning-file";
                    approvalRequest.FileName = "2026-05-23_기획팀_부스운영안.pdf";
                    approvalRequest.MimeType = "application/pdf";
                    approvalRequest.SourceFolderId = "planning-materials-folder";
                    approvalRequest.SourceFolderKind = EDriveFolderKind.TeamMaterials;
                    approvalRequest.TeamId = 1;
                    approvalRequest.TeamName = "기획팀";
                    approvalRequest.RequesterUserId = 1;
                    approvalRequest.RequesterDisplayName = "기획팀원";
                    approvalRequest.RequestedAt = System.DateTime.UtcNow.AddMinutes(-10);
                    approvalRequest.Status = EApprovalRequestStatus.Approved;
                    approvalRequest.StepCount = 1;
                    approvalRequest.CurrentStepNumber = 1;
                    approvalRequest.ApprovedFileId = "planning-file";
                    approvalRequest.ApprovedFolderId = "outside-approved-folder";
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                ApprovalRequestFormModel missingFinalApproverFormModel = new ApprovalRequestFormModel();
                missingFinalApproverFormModel.Title = "부스 운영안";
                missingFinalApproverFormModel.SourceFolderId = "planning-materials-folder";
                missingFinalApproverFormModel.DestinationFolderId = "planning-approved-folder";
                missingFinalApproverFormModel.FileId = "planning-file";
                missingFinalApproverFormModel.StepCount = 2;
                missingFinalApproverFormModel.FirstApproverUserId = 2;
                ApprovalRequestFormModel selectedFinalApproverFormModel = new ApprovalRequestFormModel();
                selectedFinalApproverFormModel.Title = "부스 운영안";
                selectedFinalApproverFormModel.SourceFolderId = "planning-materials-folder";
                selectedFinalApproverFormModel.DestinationFolderId = "planning-approved-folder";
                selectedFinalApproverFormModel.FileId = "planning-file";
                selectedFinalApproverFormModel.StepCount = 2;
                selectedFinalApproverFormModel.FirstApproverUserId = 2;
                selectedFinalApproverFormModel.FinalApproverUserId = 7;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);
                ServiceResult missingFinalApproverResult = await approvalService.SubmitRequestAsync(requesterSession, missingFinalApproverFormModel.CreateCommand(), CancellationToken.None);
                ServiceResult selectedFinalApproverResult = await approvalService.SubmitRequestAsync(requesterSession, selectedFinalApproverFormModel.CreateCommand(), CancellationToken.None);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "부스 운영안";
                formModel.SourceFolderId = "planning-materials-folder";
                formModel.DestinationFolderId = "planning-approved-folder";
                formModel.FileId = "planning-file";
                formModel.StepCount = 2;
                formModel.FirstApproverUserId = 2;

                ServiceResult submitResult = await approvalService.SubmitRequestAsync(requesterSession, formModel.CreateCommand(), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> leaderApprovals = await approvalService.GetPendingApprovalsAsync(responsibleSession, CancellationToken.None);
                ServiceResult rejectResult = await approvalService.RejectRequestAsync(responsibleSession, ApprovalRequestId.CreateExisting(leaderApprovals[0].Id), ApprovalComment.CreateOptional("보완 필요"), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> requesterHistory = await approvalService.GetRelatedHistoryAsync(requesterSession, CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> rejecterHistory = await approvalService.GetRelatedHistoryAsync(responsibleSession, CancellationToken.None);

                Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession responsibleSession = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);
                UserSession managerSession = createSession(3, "manager", "관리팀 매니저", EStoredCompatibilityRole.ManagementManager, null);
                ApprovalRequestFormModel formModel = new ApprovalRequestFormModel();
                formModel.Title = "책임자 단독 상신";
                formModel.SourceFolderId = "planning-materials-folder";
                formModel.DestinationFolderId = "planning-approved-folder";
                formModel.FileId = "planning-file";
                formModel.StepCount = 1;
                formModel.FinalApproverUserId = 3;

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(responsibleSession, CancellationToken.None);
                ServiceResult submitResult = await approvalService.SubmitRequestAsync(responsibleSession, formModel.CreateCommand(), CancellationToken.None);
                IReadOnlyList<ApprovalRequestItem> finalApproverApprovals = await approvalService.GetPendingApprovalsAsync(managerSession, CancellationToken.None);

                Assert.Contains(new ApprovalStepCount(1), createContext.AvailableStepCounts);
                Assert.True(submitResult.IsSuccess, submitResult.ErrorMessage);
                Assert.Single(finalApproverApprovals);
            }
        }
    }

    [Fact]
    public async Task ApprovalCandidatesComeFromConfiguredApprovalPermissionsAsync()
    {
        using (TestDbContextFactory dbContextFactory = createApprovalDbContextFactory(EFinalApproverSeedMode.SecondFinalApprover))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createApprovalDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession requesterSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);

                ApprovalCreateContext createContext = await approvalService.GetCreateContextAsync(requesterSession, CancellationToken.None);

                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 2);
                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 4);
                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 5);
                Assert.Contains(createContext.ApproverOptions, option => option.UserId == 6);
                Assert.DoesNotContain(createContext.ApproverOptions, option => option.UserId == 8);
                Assert.Equal(new int[] { 3, 7 }, createContext.FinalApproverOptions.Select(option => option.UserId).ToArray());
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                ApprovalService approvalService = new ApprovalService(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory));
                UserSession teamMemberSession = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
                UserSession teamLeaderSession = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);

                ApprovalCreateContext memberCreateContext = await approvalService.GetCreateContextAsync(teamMemberSession, CancellationToken.None);
                ApprovalCreateContext leaderCreateContext = await approvalService.GetCreateContextAsync(teamLeaderSession, CancellationToken.None);
                ApprovalStepCount threeStepApproval = new ApprovalStepCount(3);
                ApprovalDefaultLineOption memberDefaultLine = memberCreateContext.DefaultLineOptions.Single(defaultLine => defaultLine.StepCount == threeStepApproval);
                ApprovalDefaultLineOption leaderDefaultLine = leaderCreateContext.DefaultLineOptions.Single(defaultLine => defaultLine.StepCount == threeStepApproval);

                Assert.Equal(2, memberDefaultLine.FirstApproverUserId);
                Assert.Equal(4, memberDefaultLine.SecondApproverUserId);
                Assert.Equal(3, memberDefaultLine.FinalApproverUserId);
                Assert.Equal(4, leaderDefaultLine.FirstApproverUserId);
                Assert.Equal(5, leaderDefaultLine.SecondApproverUserId);
                Assert.Equal(3, leaderDefaultLine.FinalApproverUserId);
            }
        }
    }

}
