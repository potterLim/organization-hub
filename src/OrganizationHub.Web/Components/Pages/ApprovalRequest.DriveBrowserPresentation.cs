using System;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private string getDestinationSummaryPath()
    {
        return string.Join(" / ", mDestinationFolderPath.Select(item => item.Name));
    }

    private string getFileCountText()
    {
        int folderCount = mFiles.Count(file => file.IsFolder);
        int fileCount = mFiles.Count - folderCount;
        return "폴더 " + folderCount.ToString(CultureInfo.InvariantCulture) + "개 · 파일 " + fileCount.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private string getFileEmptyMessage()
    {
        if (string.IsNullOrWhiteSpace(mKeyword) == false)
        {
            return "검색 결과가 없습니다.";
        }

        return "결재할 파일을 선택해 주세요.";
    }

    private string getFileEmptyDescription()
    {
        if (string.IsNullOrWhiteSpace(mKeyword) == false)
        {
            return "검색어를 바꾸거나 현재 위치를 새로고침해 주세요.";
        }

        return "목록에 파일이 보이면 선택한 뒤 결재 요청을 보낼 수 있습니다.";
    }

    private string getDestinationFolderCountText()
    {
        return "폴더 " + mDestinationFoldersInCurrentFolder.Count.ToString(CultureInfo.InvariantCulture) + "개 · 파일 " + mDestinationFilesInCurrentFolder.Count.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private bool shouldShowSourceFolderPath()
    {
        return mSourceFolderPath.Count > 1;
    }

    private bool shouldShowDestinationFolderPath()
    {
        return mDestinationFolderPath.Count > 1;
    }

    private string formatDate(DateTime? dateTimeOrNull)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return PresentationDateTimeFormatter.formatClientDateTime(dateTimeOrNull.Value, ClientTimeZoneInfo, PresentationDateTimeFormatter.FULL_DATE_TIME_FORMAT);
    }

    private string getFileIcon(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return Icons.Material.Filled.Folder;
        }

        if (file.MimeType == GoogleMimeTypes.PDF)
        {
            return Icons.Material.Filled.PictureAsPdf;
        }

        return Icons.Material.Filled.InsertDriveFile;
    }

    private string getFileIconClass(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return "hub-file-icon hub-file-folder-icon";
        }

        return "hub-file-icon";
    }
}
