using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
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
    private string mSelectedRootFolderID = string.Empty;
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
            mSelectedRootFolderID = defaultRootFolder.FolderID;
            resetPath(defaultRootFolder);
            await loadFoldersAsync();
        }

        await loadRecentUploadsAsync();
        mIsLoadingFolders = false;
    }

    private async Task handleRootFolderChangedAsync(string folderID)
    {
        mSelectedRootFolderID = folderID;
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
            mCurrentFolderUploadDecision = await PermissionService.DecideUploadToFolderAsync(CurrentUserSessionOrNull, getCurrentFolderID(), CancellationToken.None);
            mCurrentFolderCreateDecision = await PermissionService.DecideCreateFolderInFolderAsync(CurrentUserSessionOrNull, getCurrentFolderID(), CancellationToken.None);
            if (mCurrentFolderCreateDecision.IsAllowed == false)
            {
                closeFolderCreate();
            }

            if (mCurrentFolderUploadDecision.IsAllowed == false)
            {
                mFoldersInCurrentFolder = new List<DriveFileItem>();
                return;
            }

            IReadOnlyList<DriveFileItem> files = await DriveBrowserService.GetFolderFilesAsync(CurrentUserSessionOrNull, rootFolderOrNull.FolderKind, rootFolderOrNull.FolderID, getCurrentFolderID(), string.Empty, CancellationToken.None);
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
        pathItem.ID = folder.ID;
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

        PermissionDecision createDecision = await PermissionService.DecideCreateFolderInFolderAsync(CurrentUserSessionOrNull, getCurrentFolderID(), CancellationToken.None);
        if (createDecision.IsAllowed == false)
        {
            showMessage(createDecision.Reason, Severity.Error);
            return;
        }

        mIsCreatingFolder = true;
        try
        {
            string currentFolderID = getCurrentFolderID();
            DriveFileItem createdFolder = await DriveService.CreateFolderAsync(currentFolderID, folderName, CancellationToken.None);
            DriveBrowserService.InvalidateRecentFiles(currentFolderID);
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

    private void handleFileSelected(InputFileChangeEventArgs inputFileChangeEventArgs)
    {
        mSelectedFileOrNull = inputFileChangeEventArgs.File;
        mMessage = string.Empty;
    }

    private async Task uploadFileAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        if (mSelectedFileOrNull == null)
        {
            showMessage("올릴 파일을 선택해 주세요.", Severity.Warning);
            return;
        }

        DriveFolderReference? rootFolderOrNull = getSelectedRootFolderOrNull();
        if (rootFolderOrNull == null)
        {
            showMessage("파일을 올릴 위치를 선택해 주세요.", Severity.Warning);
            return;
        }

        PermissionDecision uploadDecision = await PermissionService.DecideUploadToFolderAsync(CurrentUserSessionOrNull, getCurrentFolderID(), CancellationToken.None);
        if (uploadDecision.IsAllowed == false)
        {
            showMessage(uploadDecision.Reason, Severity.Error);
            return;
        }

        EFileUploadPolicyContext uploadPolicyContext = isCurrentPathMinutesFolder() ? EFileUploadPolicyContext.MinutesFolder : EFileUploadPolicyContext.RegularFolder;
        FileUploadPolicy uploadPolicy = FilePolicyService.GetUploadPolicy(rootFolderOrNull.FolderKind, uploadPolicyContext);
        string extension = Path.GetExtension(mSelectedFileOrNull.Name).ToLowerInvariant();
        if (uploadPolicy.AllowedExtensions.Contains(extension) == false)
        {
            showMessage("이 위치에는 " + extension + " 파일을 업로드할 수 없습니다.", Severity.Error);
            return;
        }

        if (mSelectedFileOrNull.Size > uploadPolicy.MaximumUploadBytes)
        {
            showMessage("파일 크기가 제한을 초과했습니다.", Severity.Error);
            return;
        }

        mIsUploading = true;
        try
        {
            string contentType = FilePolicyService.GetContentType(mSelectedFileOrNull.Name);
            using (Stream fileStream = mSelectedFileOrNull.OpenReadStream(uploadPolicy.MaximumUploadBytes))
            {
                string currentFolderID = getCurrentFolderID();
                DriveFileItem uploadedFile = await DriveService.UploadFileAsync(currentFolderID, fileStream, mSelectedFileOrNull.Name, contentType, CancellationToken.None);
                DriveFolderReference targetFolder = new DriveFolderReference(currentFolderID, getCurrentFolderDisplayName(), rootFolderOrNull.FolderKind, rootFolderOrNull.TeamID, rootFolderOrNull.TeamKey, rootFolderOrNull.TeamName, EDriveFolderUploadAccess.UploadAllowed);
                await UploadAuditLogService.RecordUploadAsync(CurrentUserSessionOrNull, targetFolder, uploadedFile, CancellationToken.None);
                DriveBrowserService.InvalidateRecentFiles(currentFolderID);
                await loadRecentUploadsAsync();
                showMessage("파일 올리기 완료: " + uploadedFile.Name, Severity.Success);
            }
        }
        catch
        {
            showMessage("파일을 올리지 못했습니다. 파일은 저장되지 않았습니다. 잠시 후 다시 시도해 주세요.", Severity.Error);
        }

        mIsUploading = false;
    }

    private void showMessage(string message, Severity severity)
    {
        mMessage = message;
        mMessageSeverity = severity;
    }

    private async Task loadRecentUploadsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        DriveFolderReference? selectedRootFolderOrNull = getSelectedRootFolderOrNull();
        if (selectedRootFolderOrNull == null)
        {
            mRecentUploads = new List<UploadAuditLogItem>();
            return;
        }

        mIsLoadingRecentUploads = true;
        mRecentUploadsErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<UploadAuditLogItem> recentUploads = await UploadAuditLogService.GetRecentUploadsForRootAsync(CurrentUserSessionOrNull, selectedRootFolderOrNull, 6, CancellationToken.None);
            mRecentUploads = recentUploads.ToList();
        }
        catch
        {
            mRecentUploads = new List<UploadAuditLogItem>();
            mRecentUploadsErrorMessage = "최근 업로드를 불러오지 못했습니다.";
        }
        finally
        {
            mIsLoadingRecentUploads = false;
        }
    }

    private void resetPath(DriveFolderReference rootFolder)
    {
        DriveFolderPathItem pathItem = new DriveFolderPathItem();
        pathItem.ID = rootFolder.FolderID;
        pathItem.Name = rootFolder.DisplayName;
        mFolderPath = new List<DriveFolderPathItem>();
        mFolderPath.Add(pathItem);
    }

    private string getCurrentFolderID()
    {
        if (mFolderPath.Count == 0)
        {
            return mSelectedRootFolderID;
        }

        return mFolderPath[mFolderPath.Count - 1].ID;
    }

    private string getCurrentFolderDisplayName()
    {
        if (mFolderPath.Count == 0)
        {
            return "선택 위치";
        }

        return mFolderPath[mFolderPath.Count - 1].Name;
    }

    private DriveFolderReference? getSelectedRootFolderOrNull()
    {
        return mUploadRootFolders.FirstOrDefault(folder => folder.FolderID == mSelectedRootFolderID);
    }

    private string getRecentUploadsTitle()
    {
        DriveFolderReference? selectedRootFolderOrNull = getSelectedRootFolderOrNull();
        if (selectedRootFolderOrNull == null || string.IsNullOrWhiteSpace(selectedRootFolderOrNull.DisplayName))
        {
            return "최근 업로드";
        }

        return "최근 업로드 · " + selectedRootFolderOrNull.DisplayName;
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

        if (CurrentUserSessionOrNull != null && CurrentUserSessionOrNull.TeamID.HasValue)
        {
            int teamID = CurrentUserSessionOrNull.TeamID.Value;
            DriveFolderReference? teamMaterialsFolderOrNull = mUploadRootFolders.FirstOrDefault(folder => folder.FolderKind == EDriveFolderKind.TeamMaterials && folder.TeamID == teamID);
            if (teamMaterialsFolderOrNull != null)
            {
                return teamMaterialsFolderOrNull;
            }
        }

        return mUploadRootFolders[0];
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

    private bool isCurrentPathMinutesFolder()
    {
        foreach (DriveFolderPathItem pathItem in mFolderPath)
        {
            if (pathItem.Name.Contains("회의록", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string formatDate(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatFileSize(long size)
    {
        if (size < 1024)
        {
            return size.ToString(CultureInfo.InvariantCulture) + " B";
        }

        if (size < 1024 * 1024)
        {
            return (size / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        }

        return (size / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    private string getFolderCountText()
    {
        return "하위 폴더 " + mFoldersInCurrentFolder.Count.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private string getSelectedFileText()
    {
        if (mSelectedFileOrNull == null)
        {
            return "선택된 파일 없음";
        }

        return mSelectedFileOrNull.Name + " · " + formatFileSize(mSelectedFileOrNull.Size);
    }
}
