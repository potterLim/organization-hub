using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private async Task createFolderAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderName folderName = DriveFolderName.CreateOptional(mNewFolderName);
        if (folderName.HasValue == false)
        {
            showFolderMessage("새 폴더 이름을 입력해 주세요.", Severity.Warning);
            return;
        }

        PermissionDecision createDecision = await PermissionService.DecideCreateFolderInFolderAsync(CurrentUserSessionOrNull, DriveFolderId.CreateOptional(getCurrentFolderId()), CancellationToken.None);
        if (createDecision.IsAllowed == false)
        {
            showFolderMessage(createDecision.Reason, Severity.Error);
            return;
        }

        mIsCreatingFolder = true;
        try
        {
            DriveFolderId currentFolderId = DriveFolderId.CreateOptional(getCurrentFolderId());
            DriveFileItem createdFolder = await DriveService.CreateFolderAsync(currentFolderId, folderName.ToDriveItemName(), CancellationToken.None);
            DriveBrowserService.InvalidateRecentFiles(currentFolderId);
            mNewFolderName = string.Empty;
            mIsFolderCreateOpen = false;
            await openFolderAsync(createdFolder);
            showFolderMessage("새 폴더를 만들고 해당 위치로 이동했습니다.", Severity.Success);
        }
        catch
        {
            showFolderMessage("폴더를 만들지 못했습니다. 잠시 후 다시 시도해 주세요.", Severity.Error);
        }

        mIsCreatingFolder = false;
    }

    private void toggleFolderCreate()
    {
        mIsFolderCreateOpen = mIsFolderCreateOpen == false;
        if (mIsFolderCreateOpen == false)
        {
            mNewFolderName = string.Empty;
        }
    }

    private void closeFolderCreate()
    {
        mIsFolderCreateOpen = false;
        mNewFolderName = string.Empty;
    }

    private async Task handleFolderCreateKeyDownAsync(KeyboardEventArgs keyboardEventArgs)
    {
        if (keyboardEventArgs.Key == "Enter" && mIsCreatingFolder == false)
        {
            await createFolderAsync();
            return;
        }

        if (keyboardEventArgs.Key == "Escape")
        {
            closeFolderCreate();
        }
    }
}
