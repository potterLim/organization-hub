using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private async Task handleRootFolderChangedAsync(string folderID)
    {
        mSelectedRootFolderID = folderID;
        mMessage = string.Empty;
        DriveFolderReference? selectedFolderOrNull = getSelectedRootFolderOrNull();
        if (selectedFolderOrNull != null)
        {
            resetPath(selectedFolderOrNull);
        }

        await loadFoldersAsync();
        await loadRecentUploadsAsync();
    }

    private async Task loadFoldersAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? rootFolderOrNull = getSelectedRootFolderOrNull();
        if (rootFolderOrNull == null)
        {
            return;
        }

        mIsLoadingFiles = true;
        mFolderLoadErrorMessage = string.Empty;
        try
        {
            mCurrentFolderUploadDecision = await PermissionService.DecideUploadToFolderAsync(CurrentUserSessionOrNull, getCurrentFolderID(), CancellationToken.None);
            mCurrentFolderCreateDecision = await PermissionService.DecideCreateFolderInFolderAsync(CurrentUserSessionOrNull, getCurrentFolderID(), CancellationToken.None);
            if (mCurrentFolderCreateDecision.IsAllowed == false)
            {
                closeFolderCreate();
            }

            if (mCurrentFolderUploadDecision.IsAllowed == false)
            {
                mFoldersInCurrentFolder = new List<DriveFileItem>();
                return;
            }

            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(CurrentUserSessionOrNull, rootFolderOrNull.FolderKind, rootFolderOrNull.FolderID, getCurrentFolderID(), string.Empty, CancellationToken.None);
            mFoldersInCurrentFolder = files.Where(file => file.IsFolder).OrderBy(file => file.Name).ToList();
        }
        catch
        {
            mFoldersInCurrentFolder = new List<DriveFileItem>();
            mFolderLoadErrorMessage = "하위 폴더를 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingFiles = false;
        }
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
        await loadFoldersAsync();
    }

    private async Task navigateToPathAsync(int index)
    {
        if (index < 0 || index >= mFolderPath.Count)
        {
            return;
        }

        mFolderPath = mFolderPath.Take(index + 1).ToList();
        await loadFoldersAsync();
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

    private DriveFolderReference? getSelectedRootFolderOrNull()
    {
        return mUploadRootFolders.FirstOrDefault(folder => folder.FolderID == mSelectedRootFolderID);
    }

    private DriveFolderReference getDefaultUploadRootFolder()
    {
        if (CurrentUserSessionOrNull != null && CurrentUserSessionOrNull.IsManagementRole())
        {
            DriveFolderReference? managementFolderOrNull = mUploadRootFolders.FirstOrDefault(folder => folder.FolderKind == EDriveFolderKind.TeamMaterials && folder.IsManagementTeam);
            if (managementFolderOrNull != null)
            {
                return managementFolderOrNull;
            }

            return mUploadRootFolders[0];
        }

        if (CurrentUserSessionOrNull != null && CurrentUserSessionOrNull.TeamID.HasValue)
        {
            int teamID = CurrentUserSessionOrNull.TeamID.Value;
            DriveFolderReference? teamMaterialsFolderOrNull = mUploadRootFolders.FirstOrDefault(folder => folder.FolderKind == EDriveFolderKind.TeamMaterials && folder.TeamID == teamID);
            if (teamMaterialsFolderOrNull != null)
            {
                return teamMaterialsFolderOrNull;
            }
        }

        return mUploadRootFolders[0];
    }
}
