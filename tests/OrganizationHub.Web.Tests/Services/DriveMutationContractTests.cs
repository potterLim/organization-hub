using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DriveMutationContractTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LostTrashResponseIsConfirmedWithoutRepeatingTheMutationAsync(bool isCommitted)
    {
        using (DriveMutationTransport transport = new DriveMutationTransport())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            transport.ShouldCommit = isCommitted;
            GoogleDriveService drive = createDrive(transport, cache);
            if (isCommitted)
            {
                await drive.DeleteDriveItemAsync(DriveItemId.CreateRequired("file"), CancellationToken.None);
                Assert.Empty(await drive.ListFilesAsync(DriveFolderId.CreateRequired("folder"), CancellationToken.None));
            }
            else
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => drive.DeleteDriveItemAsync(DriveItemId.CreateRequired("file"), CancellationToken.None));
            }
            Assert.Equal(1, transport.Writes);
            Assert.Equal(isCommitted, transport.IsTrashed);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnreadableDeletionOutcomeRemainsUncertainAsync(bool hidesParent)
    {
        using (DriveMutationTransport transport = new DriveMutationTransport())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            transport.ShouldFailRead = hidesParent == false;
            transport.ShouldReturnMissingAfterWrite = hidesParent;
            transport.ShouldHideParent = hidesParent;
            GoogleDriveService drive = createDrive(transport, cache);
            await Assert.ThrowsAsync<ResourceDeletionUncertainException>(() => drive.DeleteDriveItemAsync(DriveItemId.CreateRequired("file"), CancellationToken.None));
            Assert.Equal(1, transport.Writes);
        }
    }

    [Fact]
    public async Task RejectedTrashRequestIsNotReportedAsSuccessfulAsync()
    {
        using (DriveMutationTransport transport = new DriveMutationTransport())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            transport.RejectedStatusOrNull = HttpStatusCode.Forbidden;
            GoogleDriveService drive = createDrive(transport, cache);
            await Assert.ThrowsAsync<Google.GoogleApiException>(() => drive.DeleteDriveItemAsync(DriveItemId.CreateRequired("file"), CancellationToken.None));
            Assert.False(transport.IsTrashed);
            Assert.Equal(1, transport.Writes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadReceiptSurvivesLostResponseAndDoesNotAcceptAnotherOperationAsync(bool isReplacement)
    {
        using (DriveMutationTransport transport = new DriveMutationTransport())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (MemoryStream content = new MemoryStream(new byte[] { 1, 2, 3 }))
        {
            transport.HasFile = isReplacement;
            GoogleDriveService drive = createDrive(transport, cache);
            DriveFolderId folderId = DriveFolderId.CreateRequired("folder");
            DriveItemId fileId = isReplacement ? DriveItemId.CreateRequired("file") : await drive.GenerateFileIdAsync(CancellationToken.None);
            DriveItemName name = DriveItemName.CreateRequired("report.pdf");
            FileContentType contentType = FileContentType.CreateOptional("application/pdf");
            DriveFileUploadRequest request = isReplacement
                ? DriveFileUploadRequest.CreateReplacementFile(folderId, content, name, contentType, fileId)
                : DriveFileUploadRequest.CreateNewFile(folderId, content, name, contentType);
            request = request.WithUploadIdentity(fileId, "upload-operation");
            await Assert.ThrowsAsync<System.InvalidOperationException>(() => drive.UploadFileAsync(request, CancellationToken.None));
            DriveFileItem? receiptOrNull = await drive.GetUploadReceiptOrNullAsync(fileId, "upload-operation", CancellationToken.None);
            Assert.NotNull(receiptOrNull);
            Assert.Equal(fileId.GetRequiredValue(), receiptOrNull.Id);
            Assert.Equal("upload-operation", transport.OperationKey);
            Assert.Null(await drive.GetUploadReceiptOrNullAsync(fileId, "different-operation", CancellationToken.None));
            Assert.Equal(1, transport.Writes);
        }
    }

    private static GoogleDriveService createDrive(DriveMutationTransport transport, MemoryCache cache)
    {
        return new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
    }
}
