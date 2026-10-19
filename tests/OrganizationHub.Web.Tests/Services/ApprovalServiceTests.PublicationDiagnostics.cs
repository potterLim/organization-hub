using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Fakes;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class ApprovalServiceTests
{
    [Fact]
    public async Task FailedPublicationExposesSafeDiagnosticsAndAdminRetryCompletesSameFileAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.DriveService.FailNextPublication(FakeDriveService.EPublicationFailure.AfterUpload);
            Assert.False((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
            grantPublicationAdministration(fixture);
            ApprovalPublicationPage pending = await fixture.CreateService().GetPendingPublicationsAsync(fixture.Manager, int.MaxValue, CancellationToken.None);
            ApprovalPublicationItem item = Assert.Single(pending.Items);
            Assert.Equal(1, pending.PageNumber);
            Assert.Equal(1, item.AttemptCount);
            Assert.NotNull(item.LastAttemptAt);
            Assert.False(item.IsProcessing);
            Assert.Contains("연결 상태", item.LastError);
            ApprovalRequestItem inboxItem = Assert.Single(await fixture.CreateService().GetPendingApprovalsAsync(fixture.Manager, CancellationToken.None));
            Assert.True(inboxItem.HasPublicationFailure);
            Assert.False(inboxItem.IsPublicationProcessing);
            string targetId;
            using (OrganizationHubDbContext db = fixture.DbContextFactory.CreateDbContext())
            {
                targetId = db.ApprovalPublications.Single().TargetFileId;
            }

            ServiceResult retry = await fixture.CreateService().RetryPublicationAsAdministratorAsync(fixture.Manager, requestId, CancellationToken.None);
            Assert.True(retry.IsSuccess, retry.ErrorMessage);
            Assert.Empty((await fixture.CreateService().GetPendingPublicationsAsync(fixture.Manager, 1, CancellationToken.None)).Items);
            using (OrganizationHubDbContext db = fixture.DbContextFactory.CreateDbContext())
            {
                ApprovalPublication completed = db.ApprovalPublications.Single();
                Assert.Equal(targetId, completed.TargetFileId);
                Assert.Equal(2, completed.AttemptCount);
                Assert.Empty(completed.LastError);
            }
            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }

    [Fact]
    public async Task PublicationAdministrationRejectsUnauthorizedUsersAndHonorsActiveLeaseAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.DriveService.FailNextPublication(FakeDriveService.EPublicationFailure.BeforeUpload);
            Assert.False((await executeRegressionDecisionAsync(fixture.CreateService(), fixture.Manager, requestId, EApprovalAuditAction.Approved)).IsSuccess);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.CreateService().GetPendingPublicationsAsync(fixture.Requester, 1, CancellationToken.None));
            Assert.False((await fixture.CreateService().RetryPublicationAsAdministratorAsync(fixture.Requester, requestId, CancellationToken.None)).IsSuccess);
            grantPublicationAdministration(fixture);
            using (OrganizationHubDbContext db = fixture.DbContextFactory.CreateDbContext())
            {
                ApprovalPublication publication = db.ApprovalPublications.Single();
                publication.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(1);
                publication.LeaseToken = "another-worker";
                await db.SaveChangesAsync();
            }

            ApprovalPublicationItem item = Assert.Single((await fixture.CreateService().GetPendingPublicationsAsync(fixture.Manager, 1, CancellationToken.None)).Items);
            Assert.True(item.IsProcessing);
            ApprovalRequestItem inboxItem = Assert.Single(await fixture.CreateService().GetPendingApprovalsAsync(fixture.Manager, CancellationToken.None));
            Assert.True(inboxItem.IsPublicationProcessing);
            Assert.False((await fixture.CreateService().RetryPublicationAsAdministratorAsync(fixture.Manager, requestId, CancellationToken.None)).IsSuccess);
            Assert.Equal(0, fixture.DriveService.PublicationWriteCount);
            using (OrganizationHubDbContext db = fixture.DbContextFactory.CreateDbContext())
            {
                Assert.Equal(1, db.ApprovalPublications.Single().AttemptCount);
            }
        }
    }

    private static void grantPublicationAdministration(ApprovalReliabilityFixture fixture)
    {
        using (OrganizationHubDbContext db = fixture.DbContextFactory.CreateDbContext())
        {
            UserAccount manager = db.UserAccounts.Include(user => user.Position).Single(user => user.Id == 3);
            Assert.NotNull(manager.Position);
            manager.Position.IsSystemAdministrator = true;
            db.SaveChanges();
        }
    }
}
