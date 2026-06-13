using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private async Task submitRequestAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        mFormModel.DestinationFolderID = getCurrentDestinationFolderID();
        mIsSubmitting = true;
        ServiceResult result = await ApprovalService.SubmitRequestAsync(CurrentUserSessionOrNull, mFormModel, CancellationToken.None);
        mIsSubmitting = false;
        if (result.IsSuccess)
        {
            mMessage = "결재 요청이 등록되었습니다.";
            mMessageSeverity = Severity.Success;
            mFormModel = new ApprovalRequestFormModel();
            mHasCustomTitle = false;
            initializeDefaults();
            await loadFilesAsync();
            await loadDestinationFoldersAsync();
            return;
        }

        mMessage = result.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }

    private string createDefaultRequestTitle(DriveFileItem file)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            fileNameWithoutExtension = file.Name;
        }

        return fileNameWithoutExtension + " 결재 요청";
    }
}
