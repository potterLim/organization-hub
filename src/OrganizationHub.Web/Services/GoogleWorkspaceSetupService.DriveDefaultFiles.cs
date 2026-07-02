using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private const string DEFAULT_NOTICE_FILE_CONTENT = "조직 공지 자료입니다.\r\n필요한 공지와 안내를 이 파일에 정리해 주세요.\r\n";
    private const string DEFAULT_WORK_GUIDELINE_FILE_CONTENT = "업무 기준 자료입니다.\r\n조직에서 함께 지킬 업무 기준을 이 파일에 정리해 주세요.\r\n";
    private const string DEFAULT_TEXT_FILE_CONTENT_TYPE = "text/plain";

    private async Task<DriveFileItem> getOrCreateDefaultDriveFileAsync(
        DriveFolderId parentFolder,
        DriveItemName fileName,
        string fileContent,
        GoogleWorkspaceResourceSetupSummary summary,
        CancellationToken cancellationToken
    )
    {
        summary.TargetCount = summary.TargetCount + 1;
        string requiredFileName = fileName.GetRequiredValue();
        IReadOnlyList<DriveFileItem> files = await mDriveService.ListFilesAsync(parentFolder, cancellationToken);
        DriveFileItem? existingFileOrNull = files.FirstOrDefault(file => file.IsFolder == false && file.Name == requiredFileName);
        if (existingFileOrNull != null)
        {
            summary.ReusedCount = summary.ReusedCount + 1;
            return existingFileOrNull;
        }

        byte[] fileBytes = Encoding.UTF8.GetBytes(fileContent);
        using (MemoryStream fileStream = new MemoryStream(fileBytes))
        {
            DriveFileUploadRequest request = DriveFileUploadRequest.CreateNewFile(
                parentFolder,
                fileStream,
                fileName,
                FileContentType.CreateOptional(DEFAULT_TEXT_FILE_CONTENT_TYPE));
            DriveFileItem createdFile = await mDriveService.UploadFileAsync(request, cancellationToken);
            summary.CreatedCount = summary.CreatedCount + 1;
            return createdFile;
        }
    }
}
