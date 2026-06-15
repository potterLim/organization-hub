using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private async Task createDestinationFolderAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        string folderName = getValidFolderNameOrEmpty(mNewDestinationFolderName);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            mMessage = "새 폴더 이름을 입력해 주세요.";
            mMessageSeverity = Severity.Warning;
            return;
        }

        DriveFolderId destinationFolderId = DriveFolderId.CreateOptional(getCurrentDestinationFolderId());
        PermissionDecision createDecision = await PermissionService.DecideStoreApprovedFileToFolderAsync(CurrentUserSessionOrNull, destinationFolderId, CancellationToken.None);
        if (createDecision.IsAllowed == false)
        {
            mMessage = createDecision.Reason;
            mMessageSeverity = Severity.Error;
            return;
        }

        mIsCreatingDestinationFolder = true;
        try
        {
            DriveFileItem createdFolder = await DriveService.CreateFolderAsync(destinationFolderId, DriveItemName.CreateRequired(folderName), CancellationToken.None);
            mNewDestinationFolderName = string.Empty;
            mIsDestinationFolderCreateOpen = false;
            await loadDestinationFoldersAsync();
            await openDestinationFolderAsync(createdFolder);
            mMessage = "새 폴더를 만들었습니다.";
            mMessageSeverity = Severity.Success;
        }
        catch
        {
            mMessage = "폴더를 만들지 못했습니다. 잠시 후 다시 시도해 주세요.";
            mMessageSeverity = Severity.Error;
        }

        mIsCreatingDestinationFolder = false;
    }

    private void toggleDestinationFolderCreate()
    {
        mIsDestinationFolderCreateOpen = mIsDestinationFolderCreateOpen == false;
        if (mIsDestinationFolderCreateOpen == false)
        {
            mNewDestinationFolderName = string.Empty;
        }
    }

    private void closeDestinationFolderCreate()
    {
        mIsDestinationFolderCreateOpen = false;
        mNewDestinationFolderName = string.Empty;
    }

    private async Task handleDestinationFolderCreateKeyDownAsync(KeyboardEventArgs keyboardEventArgs)
    {
        if (keyboardEventArgs.Key == "Enter" && mIsCreatingDestinationFolder == false)
        {
            await createDestinationFolderAsync();
            return;
        }

        if (keyboardEventArgs.Key == "Escape")
        {
            closeDestinationFolderCreate();
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
