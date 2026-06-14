using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private async Task handleRootFolderChangedAsync(string folderId)
    {
        mSelectedRootFolderId = folderId;
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
            DriveFolderId currentFolderId = DriveFolderId.CreateOptional(getCurrentFolderId());
            mCurrentFolderUploadDecision = await PermissionService.DecideUploadToFolderAsync(CurrentUserSessionOrNull, currentFolderId, CancellationToken.None);
            mCurrentFolderCreateDecision = await PermissionService.DecideCreateFolderInFolderAsync(CurrentUserSessionOrNull, currentFolderId, CancellationToken.None);
            if (mCurrentFolderCreateDecision.IsAllowed == false)
            {
                closeFolderCreate();
            }

            if (mCurrentFolderUploadDecision.IsAllowed == false)
            {
                mFoldersInCurrentFolder = new List<DriveFileItem>();
                return;
            }

            DriveFolderBrowseRequest request = new DriveFolderBrowseRequest(CurrentUserSessionOrNull, rootFolderOrNull.FolderKind, rootFolderOrNull.FolderId, currentFolderId);
            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(request, CancellationToken.None);
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
        pathItem.Id = folder.Id;
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
        pathItem.Id = rootFolder.FolderId.GetRequiredValue();
        pathItem.Name = rootFolder.DisplayName;
        mFolderPath = new List<DriveFolderPathItem>();
        mFolderPath.Add(pathItem);
    }

    private string getCurrentFolderId()
    {
        if (mFolderPath.Count == 0)
        {
            return mSelectedRootFolderId;
        }

        return mFolderPath[mFolderPath.Count - 1].Id;
    }

    private DriveFolderReference? getSelectedRootFolderOrNull()
    {
        return mUploadRootFolders.FirstOrDefault(folder => folder.FolderId.GetRequiredValue() == mSelectedRootFolderId);
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

        if (CurrentUserSessionOrNull != null && CurrentUserSessionOrNull.HasTeam)
        {
            OrganizationTeamId teamId = CurrentUserSessionOrNull.GetRequiredOrganizationTeamId();
            DriveFolderReference? teamMaterialsFolderOrNull = mUploadRootFolders.FirstOrDefault(folder => folder.FolderKind == EDriveFolderKind.TeamMaterials && folder.TeamIdOrNull == teamId);
            if (teamMaterialsFolderOrNull != null)
            {
                return teamMaterialsFolderOrNull;
            }
        }

        return mUploadRootFolders[0];
    }
}
