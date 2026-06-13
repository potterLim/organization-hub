using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private ApprovalRequest createApprovalRequest(UserSession userSession, ApprovalRequestFormModel formModel, DriveFolderReference sourceFolder, DriveFileMetadata fileMetadata, DriveFileMetadata destinationFolderMetadata)
    {
        ApprovalRequest request = new ApprovalRequest();
        request.Title = getRequestTitle(formModel.Title, fileMetadata.Name);
        request.FileID = fileMetadata.ID;
        request.FileName = fileMetadata.Name;
        request.MimeType = fileMetadata.MimeType;
        request.SourceFolderID = sourceFolder.FolderID;
        request.SourceFolderKind = sourceFolder.FolderKind;
        request.TeamID = sourceFolder.TeamID;
        request.TeamName = getSourceFolderOwnerName(sourceFolder);
        request.RequesterUserID = userSession.UserID;
        request.RequesterDisplayName = userSession.DisplayName;
        request.RequestedAt = DateTime.UtcNow;
        request.Status = EApprovalRequestStatus.Pending;
        request.StepCount = formModel.StepCount;
        request.CurrentStepNumber = 1;
        request.ApprovedFileID = string.Empty;
        request.ApprovedFolderID = formModel.DestinationFolderID.Trim();
        request.ApprovedFolderDisplayName = destinationFolderMetadata.Name;
        return request;
    }

    private ApprovalStep createApprovalStep(int stepNumber, ApprovalUserOption approver)
    {
        ApprovalStep step = new ApprovalStep();
        step.StepNumber = stepNumber;
        step.ApproverUserID = approver.UserID;
        step.ApproverDisplayName = approver.DisplayName;
        step.Status = EApprovalStepStatus.Pending;
        step.Comment = string.Empty;
        return step;
    }

    private string getRequestTitle(string title, string fileName)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return fileName;
        }

        return title.Trim();
    }

    private string getSourceFolderOwnerName(DriveFolderReference sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder.TeamName) == false)
        {
            return sourceFolder.TeamName;
        }

        return string.Empty;
    }

    private async Task<ApprovalRequest?> loadApprovalRequestOrNullAsync(OrganizationHubDbContext dbContext, int approvalRequestID, CancellationToken cancellationToken)
    {
        ApprovalRequest? requestOrNull = await dbContext.ApprovalRequests.Include(request => request.Team).Include(request => request.RequesterUser).Include(request => request.Steps).Include(request => request.AuditLogs).AsSplitQuery().FirstOrDefaultAsync(request => request.ID == approvalRequestID, cancellationToken);
        return requestOrNull;
    }

    private ApprovalStep? getCurrentStepOrNull(ApprovalRequest request, UserSession userSession)
    {
        if (request.Status != EApprovalRequestStatus.Pending)
        {
            return null;
        }

        return request.Steps.FirstOrDefault(step => step.StepNumber == request.CurrentStepNumber && step.ApproverUserID == userSession.UserID && step.Status == EApprovalStepStatus.Pending);
    }

    private async Task<ServiceResult> changeNextApproverIfRequestedAsync(OrganizationHubDbContext dbContext, ApprovalRequest request, UserSession userSession, int nextApproverUserID, CancellationToken cancellationToken)
    {
        if (nextApproverUserID <= 0)
        {
            return ServiceResult.Succeed();
        }

        int nextStepNumber = request.CurrentStepNumber + 1;
        ApprovalStep? nextStepOrNull = request.Steps.FirstOrDefault(step => step.StepNumber == nextStepNumber);
        if (nextStepOrNull == null)
        {
            return ServiceResult.Fail("다음 결재 단계를 찾을 수 없습니다.");
        }

        if (nextStepNumber >= request.StepCount)
        {
            if (nextStepOrNull.ApproverUserID == nextApproverUserID)
            {
                return ServiceResult.Succeed();
            }

            return ServiceResult.Fail("최종 결재권자는 변경할 수 없습니다.");
        }

        UserAccount? nextApproverOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.ID == nextApproverUserID && user.IsActive, cancellationToken);
        if (nextApproverOrNull == null || canUseAsChangedIntermediateApprover(request, nextApproverOrNull) == false)
        {
            return ServiceResult.Fail("변경할 결재권자를 찾을 수 없습니다.");
        }

        if (nextApproverOrNull.ID == userSession.UserID)
        {
            return ServiceResult.Fail("다음 결재권자를 자기 자신으로 지정할 수 없습니다.");
        }

        foreach (ApprovalStep step in request.Steps)
        {
            if (step.StepNumber == nextStepNumber)
            {
                continue;
            }

            if (step.ApproverUserID == nextApproverOrNull.ID)
            {
                return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
            }
        }

        string previousApproverDisplayName = nextStepOrNull.ApproverDisplayName;
        nextStepOrNull.ApproverUserID = nextApproverOrNull.ID;
        nextStepOrNull.ApproverDisplayName = nextApproverOrNull.DisplayName;
        request.AuditLogs.Add(createAuditLog(userSession, EApprovalAuditAction.ApproverChanged, nextStepNumber, "결재선 변경", previousApproverDisplayName, nextApproverOrNull.DisplayName));
        return ServiceResult.Succeed();
    }

    private bool canUseAsChangedIntermediateApprover(ApprovalRequest request, UserAccount nextApprover)
    {
        if (nextApprover.ID == request.RequesterUserID)
        {
            return false;
        }

        EUserRole requesterRole = getRequesterRole(request);
        return UserPermissionPolicy.CanUseAsIntermediateApprovalApprover(requesterRole, nextApprover.Role);
    }

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

    private EUserRole getRequesterRole(ApprovalRequest request)
    {
        if (request.RequesterUser != null)
        {
            return request.RequesterUser.Role;
        }

        return EUserRole.TeamUser;
    }

    private void bumpApprovalVersion()
    {
        if (mChangeVersionServiceOrNull == null)
        {
            return;
        }

        mChangeVersionServiceOrNull.Bump(ChangeVersionKeys.APPROVAL);
    }
}
