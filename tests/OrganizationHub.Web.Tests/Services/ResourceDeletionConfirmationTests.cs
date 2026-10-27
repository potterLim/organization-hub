using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ResourceDeletionConfirmationTests
{
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
