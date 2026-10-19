using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class GoogleApprovalPublicationTests
{
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/vnd.google-apps.document")]
    public async Task SnapshotDownloadStopsWhenContentExceedsLimitAsync(string mimeType)
    {
        DriveTransport transport = new DriveTransport();
        transport.HasFile = true;
        transport.MimeType = mimeType;
        transport.Content = new byte[OrganizationHubFileLimits.DEFAULT_MAXIMUM_UPLOAD_BYTES + 1];
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            await Assert.ThrowsAnyAsync<IOException>(() => drive.DownloadFileSnapshotAsync(DriveItemId.CreateRequired("reserved-file"), CancellationToken.None));
        }
    }
}
