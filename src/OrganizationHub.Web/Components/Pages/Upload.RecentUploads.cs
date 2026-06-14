using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
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
            IReadOnlyList<UploadAuditLogItem> recentUploads = await UploadAuditLogService.GetRecentUploadsForRootAsync(CurrentUserSessionOrNull, selectedRootFolderOrNull, new RecentItemLimit(6), CancellationToken.None);
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
}
