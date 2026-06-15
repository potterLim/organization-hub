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
            DriveFileItem copiedFile = await mDriveService.CopyFileToFolderAsync(DriveItemId.CreateOptional(request.FileId), approvedFolderId, DriveItemName.CreateRequired(request.FileName), cancellationToken);
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
        auditLog.UploaderRole = getRequesterRole(request);
        auditLog.TeamId = request.TeamId;
        auditLog.TeamName = request.TeamName;
        auditLog.UploadedAt = DateTime.UtcNow;
        return auditLog;
    }
}
