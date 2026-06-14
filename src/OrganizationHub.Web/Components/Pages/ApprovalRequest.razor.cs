using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private ApprovalCreateContext mCreateContext = new ApprovalCreateContext();
    private ApprovalRequestFormModel mFormModel = new ApprovalRequestFormModel();
    private ApprovalApproverSelectionState? mApproverSelectionStateOrNull;
    private List<DriveFolderPathItem> mSourceFolderPath = new List<DriveFolderPathItem>();
    private List<DriveFolderPathItem> mDestinationFolderPath = new List<DriveFolderPathItem>();
    private List<DriveFileItem> mFiles = new List<DriveFileItem>();
    private List<DriveFileItem> mDestinationFoldersInCurrentFolder = new List<DriveFileItem>();
    private string mSelectedDestinationRootFolderID = string.Empty;
    private string mNewDestinationFolderName = string.Empty;
    private string mKeyword = string.Empty;
    private string mMessage = string.Empty;
    private string mFileLoadErrorMessage = string.Empty;
    private string mDestinationFolderLoadErrorMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mIsLoading = true;
    private bool mIsLoadingFiles;
    private bool mIsLoadingDestinationFolders;
    private bool mIsCreatingDestinationFolder;
    private bool mIsSubmitting;
    private bool mHasCustomTitle;
    private bool mIsDestinationFolderCreateOpen;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        mCreateContext = await ApprovalService.GetCreateContextAsync(CurrentUserSessionOrNull, CancellationToken.None);
        initializeDefaults();
        if (mCreateContext.SourceFolders.Count > 0)
        {
            await loadFilesAsync();
        }

        if (mCreateContext.DestinationFolders.Count > 0)
        {
            await loadDestinationFoldersAsync();
        }

        mIsLoading = false;
    }


}
