using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private List<DriveFolderReference> mUploadRootFolders = new List<DriveFolderReference>();
    private List<DriveFolderPathItem> mFolderPath = new List<DriveFolderPathItem>();
    private List<DriveFileItem> mFoldersInCurrentFolder = new List<DriveFileItem>();
    private List<UploadAuditLogItem> mRecentUploads = new List<UploadAuditLogItem>();
    private string mSelectedRootFolderId = string.Empty;
    private string mNewFolderName = string.Empty;
    private IBrowserFile? mSelectedFileOrNull;
    private string mMessage = string.Empty;
    private string mFolderLoadErrorMessage = string.Empty;
    private string mRecentUploadsErrorMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private PermissionDecision mCurrentFolderUploadDecision = PermissionDecision.Deny("파일을 올릴 수 있는 위치가 없습니다.", "파일 올리기");
    private PermissionDecision mCurrentFolderCreateDecision = PermissionDecision.Deny("폴더를 만들 수 있는 위치가 없습니다.", "새 폴더");
    private bool mIsLoadingFolders = true;
    private bool mIsLoadingFiles;
    private bool mIsLoadingRecentUploads;
    private bool mIsCreatingFolder;
    private bool mIsUploading;
    private bool mIsFolderCreateOpen;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        IReadOnlyList<DriveFolderReference> uploadFolders = await PermissionService.GetUploadableDriveFoldersAsync(CurrentUserSessionOrNull, CancellationToken.None);
        mUploadRootFolders = uploadFolders.ToList();
        if (mUploadRootFolders.Count > 0)
        {
            DriveFolderReference defaultRootFolder = getDefaultUploadRootFolder();
            mSelectedRootFolderId = defaultRootFolder.FolderId.GetRequiredValue();
            resetPath(defaultRootFolder);
            await loadFoldersAsync();
        }

        await loadRecentUploadsAsync();
        mIsLoadingFolders = false;
    }
}
