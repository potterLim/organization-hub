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

        DriveFolderName folderName = DriveFolderName.CreateOptional(mNewDestinationFolderName);
        if (folderName.HasValue == false)
        {
            showDestinationFolderMessage("새 폴더 이름을 입력해 주세요.", Severity.Warning);
            return;
        }

        DriveFolderId destinationFolderId = DriveFolderId.CreateOptional(getCurrentDestinationFolderId());
        PermissionDecision createDecision = await PermissionService.DecideStoreApprovedFileToFolderAsync(CurrentUserSessionOrNull, destinationFolderId, CancellationToken.None);
        if (createDecision.IsAllowed == false)
        {
            showDestinationFolderMessage(createDecision.Reason, Severity.Error);
            return;
        }

        mIsCreatingDestinationFolder = true;
        try
        {
            DriveFileItem createdFolder = await DriveService.CreateFolderAsync(destinationFolderId, folderName.ToDriveItemName(), CancellationToken.None);
            mNewDestinationFolderName = string.Empty;
            mIsDestinationFolderCreateOpen = false;
            await loadDestinationFoldersAsync();
            await openDestinationFolderAsync(createdFolder);
            showDestinationFolderMessage("새 폴더를 만들고 해당 위치로 이동했습니다.", Severity.Success);
        }
        catch
        {
            showDestinationFolderMessage("폴더를 만들지 못했습니다. 잠시 후 다시 시도해 주세요.", Severity.Error);
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

    private void showDestinationFolderMessage(string message, Severity severity)
    {
        mDestinationFolderMessage = message;
        mDestinationFolderMessageSeverity = severity;
    }

    private void clearDestinationFolderMessage()
    {
        mDestinationFolderMessage = string.Empty;
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
}
