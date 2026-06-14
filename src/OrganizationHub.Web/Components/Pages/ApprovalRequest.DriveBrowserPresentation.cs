using System;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
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

        return "결재 요청할 파일이 없습니다.";
    }

    private string formatDate(DateTime? dateTimeOrNull)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return dateTimeOrNull.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string getFileIcon(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return Icons.Material.Filled.Folder;
        }

        if (file.MimeType == "application/pdf")
        {
            return Icons.Material.Filled.PictureAsPdf;
        }

        return Icons.Material.Filled.InsertDriveFile;
    }
}
