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

        string folderName = getValidFolderNameOrEmpty(mNewFolderName);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            showMessage("새 폴더 이름을 입력해 주세요.", Severity.Warning);
            return;
        }

        PermissionDecision createDecision = await PermissionService.DecideCreateFolderInFolderAsync(CurrentUserSessionOrNull, DriveFolderId.CreateOptional(getCurrentFolderId()), CancellationToken.None);
        if (createDecision.IsAllowed == false)
        {
            showMessage(createDecision.Reason, Severity.Error);
            return;
        }

        mIsCreatingFolder = true;
        try
        {
            DriveFolderId currentFolderId = DriveFolderId.CreateOptional(getCurrentFolderId());
            DriveFileItem createdFolder = await DriveService.CreateFolderAsync(currentFolderId, DriveItemName.CreateRequired(folderName), CancellationToken.None);
            DriveBrowserService.InvalidateRecentFiles(currentFolderId);
            mNewFolderName = string.Empty;
            mIsFolderCreateOpen = false;
            await openFolderAsync(createdFolder);
            showMessage("새 폴더를 만들었습니다.", Severity.Success);
        }
        catch
        {
            showMessage("폴더를 만들지 못했습니다. 잠시 후 다시 시도해 주세요.", Severity.Error);
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

    private string getValidFolderNameOrEmpty(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        string trimmedName = folderName.Trim();
        if (trimmedName.Length > 80)
        {
            return string.Empty;
        }

        if (trimmedName.Contains("/") || trimmedName.Contains("\\"))
        {
            return string.Empty;
        }

        return trimmedName;
    }
}
