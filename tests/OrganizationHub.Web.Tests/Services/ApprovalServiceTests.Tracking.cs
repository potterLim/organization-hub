using System;
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
    public async Task RequesterCanTrackCurrentApproverAndRejectedResultWithoutHistoryPermissionAsync()
    {
        using (TestDbContextFactory factory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            FakeDriveService drive = createApprovalDriveService();
            OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
            ApprovalService service = new ApprovalService(factory, new PermissionService(factory, drive, cache, permissions), drive, permissions, new ApprovalPublicationService(factory, drive, permissions));
            UserSession requester = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
            UserSession leader = createSession(2, "planning.lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1);
            UserSession finalApprover = createSession(3, "manager", "관리팀 매니저", EStoredCompatibilityRole.ManagementManager, 2);
            Assert.True((await service.SubmitRequestAsync(requester, createRegressionApprovalForm().CreateCommand(), CancellationToken.None)).IsSuccess);

            ApprovalRequestItem first = Assert.Single(await service.GetRelatedHistoryAsync(requester, CancellationToken.None));
            Assert.Equal(1, first.RequesterUserId);
            Assert.Equal(EApprovalRequestStatus.Pending, first.Status);
            Assert.Equal(2, first.Steps.Single(step => step.StepNumber == first.CurrentStepNumber).ApproverUserId);
            ApprovalRequestId requestId = ApprovalRequestId.CreateExisting(first.Id);
            Assert.True((await service.ApproveRequestAsync(leader, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("확인"), CancellationToken.None)).IsSuccess);

            ApprovalRequestItem second = Assert.Single(await service.GetRelatedHistoryAsync(requester, CancellationToken.None));
            Assert.Equal(2, second.CurrentStepNumber);
            Assert.Equal(3, second.Steps.Single(step => step.StepNumber == second.CurrentStepNumber).ApproverUserId);
            Assert.True((await service.RejectRequestAsync(finalApprover, requestId, ApprovalComment.CreateOptional("보완 요청"), CancellationToken.None)).IsSuccess);
            ApprovalRequestItem rejected = Assert.Single(await service.GetRelatedHistoryAsync(requester, CancellationToken.None));
            Assert.Equal(EApprovalRequestStatus.Rejected, rejected.Status);
            Assert.Contains(rejected.AuditLogs, log => log.Comment == "보완 요청");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrackingIncludesOwnPublishingOnceAndNeverExposesOthersPendingRequestsAsync(bool canReadAllHistory)
    {
        using (TestDbContextFactory factory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                ApprovalRequest ownPublishing = createRegressionHistoryRequest(1, 1, DateTime.UtcNow.AddYears(-1));
                ownPublishing.Status = EApprovalRequestStatus.Publishing;
                ApprovalRequest otherPending = createRegressionHistoryRequest(6, 2, DateTime.UtcNow);
                otherPending.Status = EApprovalRequestStatus.Pending;
                db.ApprovalRequests.AddRange(ownPublishing, otherPending);
                if (canReadAllHistory)
                {
                    UserAccount user = db.UserAccounts.Include(account => account.Position).Single(account => account.Id == 1);
                    Assert.NotNull(user.Position);
                    addPermissionGrant(user.Position, PermissionKeys.APPROVAL_HISTORY_READ, EPermissionScopeType.Global);
                }

                await db.SaveChangesAsync(CancellationToken.None);
            }

            FakeDriveService drive = createApprovalDriveService();
            OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
            ApprovalService service = new ApprovalService(factory, new PermissionService(factory, drive, cache, permissions), drive, permissions, new ApprovalPublicationService(factory, drive, permissions));
            UserSession requester = createSession(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1);
            ApprovalRequestItem item = Assert.Single(await service.GetRelatedHistoryAsync(requester, CancellationToken.None));
            Assert.Equal(1, item.RequesterUserId);
            Assert.Equal(EApprovalRequestStatus.Publishing, item.Status);
        }
    }
}
