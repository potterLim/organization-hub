using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components;

public partial class DriveFileBrowser
{
    private void requestDelete(DriveFileItem file)
    {
        if (file.IsFolder || mCanDeleteFiles == false)
        {
            return;
        }

        mDeleteCandidateOrNull = file;
    }

    private void cancelDelete()
    {
        if (mIsDeleting)
        {
            return;
        }

        mDeleteCandidateOrNull = null;
    }

    private async Task deleteSelectedFileAsync()
    {
        if (CurrentUserSessionOrNull == null || mDeleteCandidateOrNull == null)
        {
            return;
        }

        DriveFileItem deleteCandidate = mDeleteCandidateOrNull;
        mIsDeleting = true;
        mFileErrorMessage = string.Empty;
        try
        {
            PermissionDecision deleteDecision = await PermissionService.DecideDeleteInFolderAsync(CurrentUserSessionOrNull, getCurrentFolderID(), CancellationToken.None);
            if (deleteDecision.IsAllowed == false)
            {
                mFileErrorMessage = deleteDecision.Reason;
                return;
            }

            await DriveService.DeleteDriveItemAsync(deleteCandidate.ID, CancellationToken.None);
            DriveBrowserService.SuppressRecentUploadMerge(deleteCandidate.ID);
            DriveBrowserService.InvalidateRecentFiles(getCurrentFolderID());
            mDeleteCandidateOrNull = null;
            await loadCurrentFolderAsync();
        }
        catch
        {
            mFileErrorMessage = "파일을 삭제하지 못했습니다. 파일은 아직 남아 있습니다. 잠시 후 다시 시도해 주세요.";
        }
        finally
        {
            mIsDeleting = false;
        }
    }
}
