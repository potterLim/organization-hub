using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Domain;
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

        mFormModel.DestinationFolderId = getCurrentDestinationFolderId();
        ApprovalRequestCommand command = mFormModel.CreateCommand(getRequestTitleValue());
        mIsSubmitting = true;
        clearDestinationFolderMessage();
        ServiceResult submissionResult = await ApprovalService.SubmitRequestAsync(CurrentUserSessionOrNull, command, CancellationToken.None);
        mIsSubmitting = false;
        if (submissionResult.IsSuccess)
        {
            mMessage = "결재 요청이 등록되었습니다.";
            mMessageSeverity = Severity.Success;
            mFormModel = new ApprovalRequestFormModel();
            mSelectedFileName = string.Empty;
            mSelectedSourcePath = string.Empty;
            mSuggestedRequestTitle = string.Empty;
            mHasCustomTitle = false;
            initializeApprovalRequestDefaults();
            await loadFilesAsync();
            await loadDestinationFoldersAsync();
            return;
        }

        mMessage = submissionResult.ErrorMessage;
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

    private ApprovalRequestTitle getRequestTitleValue()
    {
        if (mFormModel.TitleValue.HasValue)
        {
            return mFormModel.TitleValue;
        }

        return ApprovalRequestTitle.CreateOptional(mSuggestedRequestTitle);
    }
}
