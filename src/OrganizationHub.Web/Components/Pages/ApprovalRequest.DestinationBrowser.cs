using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private async Task handleDestinationRootChangedAsync(string folderId)
    {
        mSelectedDestinationRootFolderId = folderId;
        clearDestinationFolderMessage();
        DriveFolderReference? folderOrNull = getSelectedDestinationRootFolderOrNull();
        if (folderOrNull != null)
        {
            resetDestinationPath(folderOrNull);
        }

        await loadDestinationFoldersAsync();
    }

    private async Task loadDestinationFoldersAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? destinationFolderOrNull = getSelectedDestinationRootFolderOrNull();
        if (destinationFolderOrNull == null)
        {
            return;
        }

        mIsLoadingDestinationFolders = true;
        mDestinationFolderLoadErrorMessage = string.Empty;
        try
        {
            DriveFolderBrowseRequest request = new DriveFolderBrowseRequest(CurrentUserSessionOrNull, destinationFolderOrNull.FolderKind, destinationFolderOrNull.FolderId, DriveFolderId.CreateOptional(getCurrentDestinationFolderId()));
            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(request, CancellationToken.None);
            mDestinationFoldersInCurrentFolder = files.Where(file => file.IsFolder).OrderBy(file => file.Name).ToList();
            mDestinationFilesInCurrentFolder = files.Where(file => file.IsFolder == false).OrderBy(file => file.Name).ToList();
        }
        catch
        {
            mDestinationFoldersInCurrentFolder = new List<DriveFileItem>();
            mDestinationFilesInCurrentFolder = new List<DriveFileItem>();
            mDestinationFolderLoadErrorMessage = "저장 위치를 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingDestinationFolders = false;
        }
    }

    private async Task openDestinationFolderAsync(DriveFileItem file)
    {
        if (file.IsFolder == false)
        {
            return;
        }

        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.Id = file.Id;
        pathItem.Name = file.Name;
        mDestinationFolderPath.Add(pathItem);
        mFormModel.DestinationFolderId = file.Id;
        clearDestinationFolderMessage();
        await loadDestinationFoldersAsync();
    }

    private async Task navigateDestinationToPathAsync(int index)
    {
        if (index < 0 || index >= mDestinationFolderPath.Count)
        {
            return;
        }

        mDestinationFolderPath = mDestinationFolderPath.Take(index + 1).ToList();
        mFormModel.DestinationFolderId = getCurrentDestinationFolderId();
        clearDestinationFolderMessage();
        await loadDestinationFoldersAsync();
    }

    private DriveFolderReference? getSelectedDestinationRootFolderOrNull()
    {
        return mCreateContext.DestinationFolders.FirstOrDefault(folder => folder.FolderId.GetRequiredValue() == mSelectedDestinationRootFolderId);
    }

    private void resetDestinationPath(DriveFolderReference folder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.Id = folder.FolderId.GetRequiredValue();
        pathItem.Name = folder.DisplayName;
        mDestinationFolderPath = new List<DriveFolderPathItem>();
        mDestinationFolderPath.Add(pathItem);
        mFormModel.DestinationFolderId = folder.FolderId.GetRequiredValue();
    }

    private string getCurrentDestinationFolderId()
    {
        if (mDestinationFolderPath.Count == 0)
        {
            return mSelectedDestinationRootFolderId;
        }

        return mDestinationFolderPath[mDestinationFolderPath.Count - 1].Id;
    }
}
