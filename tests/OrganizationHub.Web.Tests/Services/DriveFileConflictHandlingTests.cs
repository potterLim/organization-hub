using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Fakes;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DriveFileConflictHandlingTests
{
    [Fact]
    public async Task UploadFileAsyncAddsNumberedSuffixWhenSameFileNameExistsAsync()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("target-folder", "고객지원팀 자료");
        driveService.AddFile("existing-file", "고객지원 이슈 대응 계획.txt", "text/plain", "target-folder");
        using (MemoryStream contentStream = createContentStream())
        {
            DriveFileUploadRequest request = DriveFileUploadRequest.CreateNewFile(
                DriveFolderId.CreateRequired("target-folder"),
                contentStream,
                DriveItemName.CreateRequired("고객지원 이슈 대응 계획.txt"),
                FileContentType.CreateOptional("text/plain"));
            DriveFileItem uploadedFile = await driveService.UploadFileAsync(request, CancellationToken.None);

            Assert.Equal("고객지원 이슈 대응 계획 (2).txt", uploadedFile.Name);
        }
    }

    [Fact]
    public async Task CopyFileToFolderAsyncAddsNumberedSuffixWhenSameFileNameExistsAsync()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("source-folder", "상신 자료");
        driveService.AddFolder("target-folder", "결재 완료 자료");
        driveService.AddFile("source-file", "고객지원 이슈 대응 계획.txt", "text/plain", "source-folder");
        driveService.AddFile("existing-file", "고객지원 이슈 대응 계획.txt", "text/plain", "target-folder");

        DriveFileCopyRequest request = DriveFileCopyRequest.CreateNewFile(
            DriveItemId.CreateRequired("source-file"),
            DriveFolderId.CreateRequired("target-folder"),
            DriveItemName.CreateRequired("고객지원 이슈 대응 계획.txt"));
        DriveFileItem copiedFile = await driveService.CopyFileToFolderAsync(request, CancellationToken.None);

        Assert.Equal("고객지원 이슈 대응 계획 (2).txt", copiedFile.Name);
    }

    private static MemoryStream createContentStream()
    {
        byte[] contentBytes = Encoding.UTF8.GetBytes("고객지원 이슈 대응 계획");
        return new MemoryStream(contentBytes);
    }
}
