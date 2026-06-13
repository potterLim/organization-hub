using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components;

public partial class DriveFileBrowser
{
    private async Task loadRootFoldersAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        mIsLoadingRoots = true;
        mRootErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<DriveFolderReference> folders = await DriveBrowserService.GetReadableFoldersAsync(CurrentUserSessionOrNull, FolderKind, CancellationToken.None);
            mRootFolders = filterRootFolders(folders).ToList();
            if (mRootFolders.Count > 0)
            {
                mSelectedRootFolderID = mRootFolders[0].FolderID;
                resetPath(mRootFolders[0]);
                await loadCurrentFolderAsync();
            }
        }
        catch
        {
            mRootErrorMessage = ErrorMessagePrefix;
        }

        mIsLoadingRoots = false;
    }

    private async Task loadCurrentFolderAsync()
    {
        if (CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedRootFolderID))
        {
            return;
        }

        mIsLoadingFiles = true;
        mFileErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<string> changeVersionKeys = getChangeVersionKeys();
            ChangeVersionReadResult<IReadOnlyList<DriveFileItem>> readResult = await ChangeVersionService.ReadWithStableSnapshotAsync(
                changeVersionKeys,
                cancellationToken => DriveBrowserService.GetFolderFilesAsync(CurrentUserSessionOrNull, FolderKind, mSelectedRootFolderID, getCurrentFolderID(), mKeyword, cancellationToken),
                CancellationToken.None);
            mFiles = readResult.Value.ToList();
            DriveFolderReference? currentRootFolderOrNull = getCurrentRootFolderOrNull();
            mCanDeleteFiles = currentRootFolderOrNull != null && PermissionService.DecideDeleteInsideRoot(CurrentUserSessionOrNull, currentRootFolderOrNull).IsAllowed;
            mDeleteCandidateOrNull = null;
            recordLoadedChangeSnapshot(readResult.Snapshot);
        }
        catch
        {
            mFileErrorMessage = ErrorMessagePrefix;
        }

        mIsLoadingFiles = false;
    }

    private async Task handleRootFolderChangedAsync(string folderID)
    {
        mSelectedRootFolderID = folderID;
        mKeyword = string.Empty;
        DriveFolderReference? selectedFolderOrNull = mRootFolders.FirstOrDefault(folder => folder.FolderID == folderID);
        if (selectedFolderOrNull != null)
        {
            resetPath(selectedFolderOrNull);
        }

        await loadCurrentFolderAsync();
    }

    private async Task openFolderAsync(DriveFileItem folder)
    {
        if (folder.IsFolder == false)
        {
            return;
        }

        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = folder.ID;
        pathItem.Name = folder.Name;
        mFolderPath.Add(pathItem);
        mKeyword = string.Empty;
        await loadCurrentFolderAsync();
    }

    private async Task navigateToPathAsync(int index)
    {
        if (index < 0 || index >= mFolderPath.Count)
        {
            return;
        }

        mFolderPath = mFolderPath.Take(index + 1).ToList();
        mKeyword = string.Empty;
        await loadCurrentFolderAsync();
    }

    private async Task clearSearchAsync()
    {
        mKeyword = string.Empty;
        await loadCurrentFolderAsync();
    }

    private void resetPath(DriveFolderReference rootFolder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = rootFolder.FolderID;
        pathItem.Name = rootFolder.DisplayName;
        mFolderPath = new List<DriveFolderPathItem>();
        mFolderPath.Add(pathItem);
    }

    private string getCurrentFolderID()
    {
        if (mFolderPath.Count == 0)
        {
            return mSelectedRootFolderID;
        }

        return mFolderPath[mFolderPath.Count - 1].ID;
    }

    private DriveFolderReference? getCurrentRootFolderOrNull()
    {
        return mRootFolders.FirstOrDefault(folder => folder.FolderID == mSelectedRootFolderID);
    }

    private IEnumerable<DriveFolderReference> filterRootFolders(IReadOnlyList<DriveFolderReference> folders)
    {
        if (ManagementTeamOnly)
        {
            return folders.Where(folder => folder.IsManagementTeam);
        }

        if (ExcludeManagementTeam)
        {
            return folders.Where(folder => folder.IsManagementTeam == false);
        }

        return folders;
    }
}
