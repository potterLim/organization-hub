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
    private async Task<ServiceResult> copyApprovedFileIfConfiguredAsync(OrganizationHubDbContext dbContext, ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedFolderID))
        {
            request.ApprovedFileID = request.FileID;
            request.ApprovedFolderID = string.Empty;
            request.ApprovedFolderDisplayName = string.Empty;
            return ServiceResult.Succeed();
        }

        try
        {
            DriveFileItem copiedFile = await mDriveService.CopyFileToFolderAsync(request.FileID, request.ApprovedFolderID.Trim(), request.FileName, cancellationToken);
            request.ApprovedFileID = copiedFile.ID;
            dbContext.UploadAuditLogs.Add(createUploadAuditLogForApprovedCopy(request, copiedFile));
            if (mDriveBrowserServiceOrNull != null)
            {
                mDriveBrowserServiceOrNull.InvalidateRecentFiles(request.ApprovedFolderID.Trim());
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
        auditLog.FileID = copiedFile.ID;
        auditLog.FileName = copiedFile.Name;
        auditLog.FolderID = request.ApprovedFolderID.Trim();
        auditLog.FolderDisplayName = request.ApprovedFolderDisplayName;
        auditLog.FolderKind = EDriveFolderKind.TeamApproved;
        auditLog.UploaderUserID = request.RequesterUserID;
        auditLog.UploaderDisplayName = request.RequesterDisplayName;
        auditLog.UploaderRole = getRequesterRole(request);
        auditLog.TeamID = request.TeamID;
        auditLog.TeamName = request.TeamName;
        auditLog.UploadedAt = DateTime.UtcNow;
        return auditLog;
    }
}
