using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class ApprovalServiceTests
{
    [Fact]
    public async Task AmbiguousReplacementDoesNotApproveOrOverwriteAndCanAddNewInsteadAsync()
    {
        using (ApprovalReliabilityFixture fixture = new ApprovalReliabilityFixture())
        {
            ApprovalRequestId requestId = await fixture.SubmitAsync();
            await fixture.ApproveFirstStepAsync(requestId);
            fixture.DriveService.AddFile("first", "2026-05-23_기획팀_부스운영안.pdf", "application/pdf", "planning-approved-folder", "official-root");
            fixture.DriveService.AddFile("second", "2026-05-23_기획팀_부스운영안.pdf", "application/pdf", "planning-approved-folder", "official-root");
            ServiceResult replacement = await fixture.CreateService().ApproveRequestAsync(fixture.Manager, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("의견"), EDriveFileNameConflictResolution.ReplaceExistingFile, CancellationToken.None);
            Assert.False(replacement.IsSuccess);
            Assert.Equal(0, fixture.DriveService.PublicationWriteCount);
            using (OrganizationHubDbContext dbContext = fixture.DbContextFactory.CreateDbContext())
            {
                Assert.Empty(dbContext.ApprovalPublications);
                Assert.Equal(EApprovalRequestStatus.Pending, dbContext.ApprovalRequests.Single().Status);
            }

            ServiceResult addition = await fixture.CreateService().ApproveRequestAsync(fixture.Manager, requestId, ApprovalUserId.Empty, ApprovalComment.CreateOptional("의견"), EDriveFileNameConflictResolution.AddNewFile, CancellationToken.None);
            Assert.True(addition.IsSuccess, addition.ErrorMessage);
            await fixture.AssertCompletedOnceAsync(requestId);
        }
    }
}
