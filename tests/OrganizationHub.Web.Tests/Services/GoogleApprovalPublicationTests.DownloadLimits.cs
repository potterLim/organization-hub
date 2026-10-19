using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class GoogleApprovalPublicationTests
{
    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public async Task SnapshotDownloadRespectsCustomLimitAndAllowsExactSizeAsync(long maximumBytes, bool shouldSucceed)
    {
        DriveTransport transport = new DriveTransport();
        transport.HasFile = true;
        transport.Content = new byte[5];
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            if (shouldSucceed)
            {
                FileDownloadResult file = await drive.DownloadFileSnapshotAsync(DriveItemId.CreateRequired("reserved-file"), new FileByteLimit(maximumBytes), CancellationToken.None);
                using (Stream content = file.ContentStream)
                {
                    Assert.Equal(5, content.Length);
                }
            }
            else
            {
                await Assert.ThrowsAsync<FileSizeLimitExceededException>(() => drive.DownloadFileSnapshotAsync(DriveItemId.CreateRequired("reserved-file"), new FileByteLimit(maximumBytes), CancellationToken.None));
            }
        }
    }

    [Fact]
    public async Task KnownOversizeMetadataIsRejectedBeforeDownloadingAsync()
    {
        DriveTransport transport = new DriveTransport();
        transport.HasFile = true;
        transport.Content = new byte[1];
        transport.ReportedSizeOrNull = 11;
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            await Assert.ThrowsAsync<FileSizeLimitExceededException>(() => drive.DownloadFileSnapshotAsync(DriveItemId.CreateRequired("reserved-file"), new FileByteLimit(10), CancellationToken.None));
            Assert.Equal(0, transport.DownloadCount);
        }
    }

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
