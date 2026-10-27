using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ResourceDeletionConfirmationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingItemRequiresAReadableContainerBeforeConfirmingDeletionAsync(bool canReadContainer)
    {
        Google.GoogleApiException notFound = new Google.GoogleApiException("calendar", "Not Found");
        notFound.HttpStatusCode = HttpStatusCode.NotFound;
        bool checkedContainer = false;
        Task confirmContainerAsync()
        {
            checkedContainer = true;
            if (canReadContainer == false)
            {
                throw notFound;
            }
            return Task.CompletedTask;
        }
        if (canReadContainer)
        {
            Assert.True(await ResourceDeletionConfirmation.IsDeletedAsync(() => throw notFound, confirmContainerAsync));
        }
        else
        {
            await Assert.ThrowsAsync<Google.GoogleApiException>(() => ResourceDeletionConfirmation.IsDeletedAsync(() => throw notFound, confirmContainerAsync));
        }
        Assert.True(checkedContainer);
    }

    [Fact]
    public async Task NotFoundDuringDeleteDoesNotProveTheItemWasDeletedAsync()
    {
        Google.GoogleApiException notFound = new Google.GoogleApiException("calendar", "Resource or calendar unavailable");
        notFound.HttpStatusCode = HttpStatusCode.NotFound;
        await Assert.ThrowsAsync<ResourceDeletionUncertainException>(() => ResourceDeletionConfirmation.ExecuteAsync(
            () => throw notFound,
            () => throw new IOException("Calendar access unavailable"), CancellationToken.None));
    }

    [Fact]
    public async Task FailedConfirmationLeavesOutcomeUncertainAsync()
    {
        await Assert.ThrowsAsync<ResourceDeletionUncertainException>(() => ResourceDeletionConfirmation.ExecuteAsync(
            () => throw new IOException("Lost response"),
            () => throw new IOException("Unavailable read"), CancellationToken.None));
    }

    [Fact]
    public async Task StillPresentItemRetainsOriginalFailureAsync()
    {
        await Assert.ThrowsAsync<IOException>(() => ResourceDeletionConfirmation.ExecuteAsync(
            () => throw new IOException("Rejected request"),
            () => Task.FromResult(false), CancellationToken.None));
    }

    [Fact]
    public async Task EditConflictDoesNotBecomeSuccessfulDeletionAsync()
    {
        bool isRead = false;
        await Assert.ThrowsAsync<ResourceEditConflictException>(() => ResourceDeletionConfirmation.ExecuteAsync(
            () => throw new ResourceEditConflictException(), readAsync, CancellationToken.None));
        Assert.False(isRead);
        Task<bool> readAsync()
        {
            isRead = true;
            return Task.FromResult(true);
        }
    }
}
