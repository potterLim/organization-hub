using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private const int RECENT_UPLOAD_MAXIMUM_COUNT = 6;

    private async Task loadRecentUploadsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? selectedRootFolderOrNull = getSelectedRootFolderOrNull();
        if (selectedRootFolderOrNull == null)
        {
            mRecentUploads = new List<UploadAuditLogItem>();
            return;
        }

        mIsLoadingRecentUploads = true;
        mRecentUploadsErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<UploadAuditLogItem> recentUploads = await UploadAuditLogService.GetRecentUploadsForRootAsync(CurrentUserSessionOrNull, selectedRootFolderOrNull, new RecentItemLimit(RECENT_UPLOAD_MAXIMUM_COUNT), CancellationToken.None);
            mRecentUploads = recentUploads.ToList();
        }
        catch
        {
            mRecentUploads = new List<UploadAuditLogItem>();
            mRecentUploadsErrorMessage = "최근 업로드를 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingRecentUploads = false;
        }
    }

    private string getRecentUploadsTitle()
    {
        DriveFolderReference? selectedRootFolderOrNull = getSelectedRootFolderOrNull();
        if (selectedRootFolderOrNull == null || string.IsNullOrWhiteSpace(selectedRootFolderOrNull.DisplayName))
        {
            return "최근 업로드";
        }

        return "최근 업로드 · " + selectedRootFolderOrNull.DisplayName;
    }

    private string getRecentUploadLocationText(UploadAuditLogItem auditLog)
    {
        return getRelativeFolderDisplayName(auditLog.FolderDisplayName);
    }

    private string getRelativeFolderDisplayName(string folderDisplayName)
    {
        string normalizedFolderDisplayName = folderDisplayName.Trim();
        DriveFolderReference? selectedRootFolderOrNull = getSelectedRootFolderOrNull();
        if (selectedRootFolderOrNull == null || string.IsNullOrWhiteSpace(normalizedFolderDisplayName))
        {
            return normalizedFolderDisplayName;
        }

        string normalizedRootDisplayName = selectedRootFolderOrNull.DisplayName.Trim();
        if (string.Equals(normalizedFolderDisplayName, normalizedRootDisplayName, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        string rootPrefix = normalizedRootDisplayName + " / ";
        if (normalizedFolderDisplayName.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            return normalizedFolderDisplayName.Substring(rootPrefix.Length);
        }

        return normalizedFolderDisplayName;
    }
}
