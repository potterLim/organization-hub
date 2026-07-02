using System;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private async Task<ServiceResult> copyApprovedFileIfConfiguredAsync(
        OrganizationHubDbContext dbContext,
        ApprovalRequest request,
        EDriveFileNameConflictResolution conflictResolution,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedFolderId))
        {
            request.ApprovedFileId = request.FileId;
            request.ApprovedFolderId = string.Empty;
            request.ApprovedFolderDisplayName = string.Empty;
            return ServiceResult.Succeed();
        }

        try
        {
            DriveFolderId approvedFolderId = DriveFolderId.CreateOptional(request.ApprovedFolderId);
            DriveItemName approvedFileName = DriveItemName.CreateRequired(request.FileName);
            DriveItemId replacementFileId = DriveItemId.Empty;
            if (mDriveFileNameConflictServiceOrNull != null)
            {
                DriveFileNameConflictCheckResult conflictCheckResult = await mDriveFileNameConflictServiceOrNull.CheckFileNameConflictAsync(
                    approvedFolderId,
                    approvedFileName,
                    cancellationToken);
                approvedFileName = getApprovedFileName(approvedFileName, conflictCheckResult, conflictResolution);
                replacementFileId = getReplacementFileId(conflictCheckResult, conflictResolution);
            }

            DriveFileCopyRequest copyRequest = createDriveFileCopyRequest(
                DriveItemId.CreateOptional(request.FileId),
                approvedFolderId,
                approvedFileName,
                conflictResolution,
                replacementFileId);
            DriveFileItem copiedFile = await mDriveService.CopyFileToFolderAsync(copyRequest, cancellationToken);
            request.ApprovedFileId = copiedFile.Id;
            dbContext.UploadAuditLogs.Add(createUploadAuditLogForApprovedCopy(request, copiedFile));
            if (mDriveBrowserServiceOrNull != null)
            {
                mDriveBrowserServiceOrNull.InvalidateRecentFiles(approvedFolderId);
            }

            return ServiceResult.Succeed();
        }
        catch (Exception exception)
        {
            return ServiceResult.Fail("결재 완료 자료 폴더로 파일을 복사하지 못했습니다. " + exception.Message);
        }
    }

    private static DriveItemName getApprovedFileName(
        DriveItemName requestedFileName,
        DriveFileNameConflictCheckResult conflictCheckResult,
        EDriveFileNameConflictResolution conflictResolution)
    {
        if (conflictResolution == EDriveFileNameConflictResolution.AddNewFile && conflictCheckResult.HasConflict)
        {
            return conflictCheckResult.SuggestedNewFileName;
        }

        return requestedFileName;
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

    private static DriveFileCopyRequest createDriveFileCopyRequest(
        DriveItemId sourceFileId,
        DriveFolderId approvedFolderId,
        DriveItemName approvedFileName,
        EDriveFileNameConflictResolution conflictResolution,
        DriveItemId replacementFileId)
    {
        if (conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile)
        {
            return DriveFileCopyRequest.CreateReplacementFile(
                sourceFileId,
                approvedFolderId,
                approvedFileName,
                replacementFileId);
        }

        return DriveFileCopyRequest.CreateNewFile(sourceFileId, approvedFolderId, approvedFileName);
    }

    private UploadAuditLog createUploadAuditLogForApprovedCopy(ApprovalRequest request, DriveFileItem copiedFile)
    {
        UploadAuditLog auditLog = new UploadAuditLog();
        auditLog.FileId = copiedFile.Id;
        auditLog.FileName = copiedFile.Name;
        auditLog.FolderId = request.ApprovedFolderId.Trim();
        auditLog.FolderDisplayName = request.ApprovedFolderDisplayName;
        auditLog.FolderKind = EDriveFolderKind.TeamApproved;
        auditLog.UploaderUserId = request.RequesterUserId;
        auditLog.UploaderDisplayName = request.RequesterDisplayName;
        auditLog.UploaderStoredCompatibilityRole = getRequesterRole(request);
        auditLog.UploaderTeamId = getRequesterTeamIdOrNull(request);
        auditLog.UploaderTeamName = getRequesterTeamName(request);
        auditLog.UploaderPositionId = getRequesterPositionIdOrNull(request);
        auditLog.UploaderPositionName = getRequesterPositionName(request);
        auditLog.TeamId = request.TeamId;
        auditLog.TeamName = request.TeamName;
        auditLog.UploadedAt = DateTime.UtcNow;
        return auditLog;
    }
}
