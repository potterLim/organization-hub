using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components;

public partial class DriveFileBrowser
{
    private string mDeleteErrorMessage = string.Empty;

    private void requestDelete(DriveFileItem file)
    {
        if (file.IsFolder || mCanDeleteFiles == false)
        {
            return;
        }

        mDeleteCandidateOrNull = file;
        mDeleteErrorMessage = string.Empty;
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
        mDeleteErrorMessage = string.Empty;
        try
        {
            DriveFolderId currentFolderId = DriveFolderId.CreateOptional(getCurrentFolderId());
            PermissionDecision deleteDecision = await PermissionService.DecideDeleteInFolderAsync(CurrentUserSessionOrNull, currentFolderId, CancellationToken.None);
            if (deleteDecision.IsAllowed == false)
            {
                mFileErrorMessage = deleteDecision.Reason;
                return;
            }

            await DriveService.DeleteDriveItemAsync(DriveItemId.CreateOptional(deleteCandidate.Id), CancellationToken.None);
            DriveBrowserService.SuppressRecentUploadMerge(DriveItemId.CreateOptional(deleteCandidate.Id));
            DriveBrowserService.InvalidateRecentFiles(currentFolderId);
            mDeleteCandidateOrNull = null;
            await loadCurrentFolderAsync();
        }
        catch (ResourceDeletionUncertainException exception)
        {
            Logger.LogWarning(exception, "Drive deletion could not be confirmed for {FileId}.", deleteCandidate.Id);
            mDeleteCandidateOrNull = null;
            mDeleteErrorMessage = "삭제 결과를 확인하지 못했습니다. 목록을 새로고침해 확인해 주세요.";
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Drive deletion failed for {FileId}.", deleteCandidate.Id);
            mDeleteCandidateOrNull = null;
            mDeleteErrorMessage = "파일을 삭제하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }
        finally
        {
            mIsDeleting = false;
        }
    }
}
