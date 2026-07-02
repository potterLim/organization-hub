using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Upload
{
    private void handleFileSelected(InputFileChangeEventArgs inputFileChangeEventArgs)
    {
        mSelectedFileOrNull = inputFileChangeEventArgs.File;
        mMessage = string.Empty;
        mUploadConflictOrNull = null;
    }

    private async Task uploadFileAsync()
    {
        await uploadFileAsync(null);
    }

    private async Task uploadFileWithConflictResolutionAsync(EDriveFileNameConflictResolution conflictResolution)
    {
        await uploadFileAsync(conflictResolution);
    }

    private async Task uploadFileAsync(EDriveFileNameConflictResolution? conflictResolutionOrNull)
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        clearFolderMessage();
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

        DriveFolderId currentFolderId = DriveFolderId.CreateOptional(getCurrentFolderId());
        PermissionDecision uploadDecision = await PermissionService.DecideUploadToFolderAsync(CurrentUserSessionOrNull, currentFolderId, CancellationToken.None);
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
            DriveItemName selectedFileName = DriveItemName.CreateRequired(mSelectedFileOrNull.Name);
            FileContentType contentType = FilePolicyService.GetContentType(selectedFileName);
            string currentFolderIdValue = getCurrentFolderId();
            DriveFolderId uploadFolderId = DriveFolderId.CreateOptional(currentFolderIdValue);
            DriveFileNameConflictCheckResult conflictCheckResult = await DriveFileNameConflictService.CheckFileNameConflictAsync(uploadFolderId, selectedFileName, CancellationToken.None);
            if (conflictCheckResult.HasConflict && conflictResolutionOrNull.HasValue == false)
            {
                mUploadConflictOrNull = conflictCheckResult;
                mIsUploading = false;
                return;
            }

            using (Stream fileStream = mSelectedFileOrNull.OpenReadStream(uploadPolicy.MaximumUploadBytes))
            {
                EDriveFileNameConflictResolution conflictResolution = EDriveFileNameConflictResolution.AddNewFile;
                if (conflictResolutionOrNull.HasValue)
                {
                    conflictResolution = conflictResolutionOrNull.GetValueOrDefault();
                }

                DriveItemName uploadFileName = getResolvedUploadFileName(selectedFileName, conflictCheckResult, conflictResolution);
                DriveItemId replacementFileId = getReplacementFileId(conflictCheckResult, conflictResolution);
                DriveFileUploadRequest request = createDriveFileUploadRequest(
                    uploadFolderId,
                    fileStream,
                    uploadFileName,
                    contentType,
                    conflictResolution,
                    replacementFileId);
                DriveFileItem uploadedFile = await DriveService.UploadFileAsync(request, CancellationToken.None);
                DriveFolderOwnerContext ownerContext = new DriveFolderOwnerContext(rootFolderOrNull.TeamIdOrNull, rootFolderOrNull.TeamKey, rootFolderOrNull.TeamName);
                DriveFolderReferenceCreationRequest referenceRequest = new DriveFolderReferenceCreationRequest(uploadFolderId, getCurrentFolderDisplayName(), rootFolderOrNull.FolderKind, EDriveFolderUploadAccess.UploadAllowed, ownerContext);
                DriveFolderReference targetFolder = new DriveFolderReference(referenceRequest);
                await UploadAuditLogService.RecordUploadAsync(CurrentUserSessionOrNull, targetFolder, uploadedFile, CancellationToken.None);
                DriveBrowserService.InvalidateRecentFiles(uploadFolderId);
                await loadFoldersAsync();
                await loadRecentUploadsAsync();
                mUploadConflictOrNull = null;
                showMessage(getUploadSuccessMessage(conflictResolution, uploadedFile.Name), Severity.Success);
            }
        }
        catch
        {
            showMessage("파일을 올리지 못했습니다. 파일은 저장되지 않았습니다. 잠시 후 다시 시도해 주세요.", Severity.Error);
        }

        mIsUploading = false;
    }

    private static DriveItemName getResolvedUploadFileName(
        DriveItemName selectedFileName,
        DriveFileNameConflictCheckResult conflictCheckResult,
        EDriveFileNameConflictResolution conflictResolution)
    {
        if (conflictResolution == EDriveFileNameConflictResolution.AddNewFile && conflictCheckResult.HasConflict)
        {
            return conflictCheckResult.SuggestedNewFileName;
        }

        return selectedFileName;
    }

    private static DriveItemId getReplacementFileId(
        DriveFileNameConflictCheckResult conflictCheckResult,
        EDriveFileNameConflictResolution conflictResolution)
    {
        if (conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile && conflictCheckResult.HasConflict)
        {
            return conflictCheckResult.GetFirstExistingFileId();
        }

        return DriveItemId.Empty;
    }

    private static DriveFileUploadRequest createDriveFileUploadRequest(
        DriveFolderId uploadFolderId,
        Stream fileStream,
        DriveItemName uploadFileName,
        FileContentType contentType,
        EDriveFileNameConflictResolution conflictResolution,
        DriveItemId replacementFileId)
    {
        if (conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile)
        {
            return DriveFileUploadRequest.CreateReplacementFile(
                uploadFolderId,
                fileStream,
                uploadFileName,
                contentType,
                replacementFileId);
        }

        return DriveFileUploadRequest.CreateNewFile(
            uploadFolderId,
            fileStream,
            uploadFileName,
            contentType);
    }

    private string getUploadSuccessMessage(EDriveFileNameConflictResolution conflictResolution, string fileName)
    {
        if (conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile)
        {
            return "기존 파일을 교체했습니다: " + fileName;
        }

        return "파일 올리기 완료: " + fileName;
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
}
