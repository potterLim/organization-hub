using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private async Task approveRequestAsync(ApprovalRequestItem approvalRequest)
    {
        await approveRequestAsync(approvalRequest, null);
    }

    private async Task approveRequestWithConflictResolutionAsync(
        ApprovalRequestItem approvalRequest,
        EDriveFileNameConflictResolution conflictResolution)
    {
        await approveRequestAsync(approvalRequest, conflictResolution);
    }

    private Task approveRequestAsync(ApprovalRequestItem approvalRequest, EDriveFileNameConflictResolution? conflictResolutionOrNull)
    {
        return runApprovalOperationAsync(approvalRequest, () => executeApprovalAsync(approvalRequest, conflictResolutionOrNull));
    }

    private async Task runApprovalOperationAsync(ApprovalRequestItem approvalRequest, Func<Task> operation)
    {
        if (mProcessingApprovalIds.Add(approvalRequest.Id) == false)
        {
            return;
        }

        mErrorsByApprovalId.Remove(approvalRequest.Id);
        mMessage = string.Empty;
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Approval operation failed for {ApprovalId}.", approvalRequest.Id);
            mErrorsByApprovalId[approvalRequest.Id] = "결재 처리를 확인하지 못했습니다. 입력한 의견은 유지됩니다. 잠시 후 다시 시도해 주세요.";
        }
        finally
        {
            mProcessingApprovalIds.Remove(approvalRequest.Id);
        }
    }

    private async Task executeApprovalAsync(ApprovalRequestItem approvalRequest, EDriveFileNameConflictResolution? conflictResolutionOrNull)
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        ApprovalRequestId approvalRequestId = ApprovalRequestId.CreateExisting(approvalRequest.Id);
        if (conflictResolutionOrNull.HasValue == false && approvalRequest.Status != EApprovalRequestStatus.Publishing)
        {
            bool shouldStopForConflict = await tryShowApprovedFileConflictAsync(approvalRequest);
            if (shouldStopForConflict)
            {
                return;
            }
        }

        ApprovalComment comment = ApprovalComment.CreateOptional(getCommentValue(approvalRequestId));
        EDriveFileNameConflictResolution conflictResolution = EDriveFileNameConflictResolution.AddNewFile;
        if (conflictResolutionOrNull.HasValue)
        {
            conflictResolution = conflictResolutionOrNull.GetValueOrDefault();
        }

        ServiceResult approvalResult = await ApprovalService.ApproveRequestAsync(
            CurrentUserSessionOrNull,
            approvalRequestId,
            ApprovalUserId.Empty,
            comment,
            conflictResolution,
            CancellationToken.None);
        showOperationResult(approvalResult, "승인되었습니다.");
        if (approvalResult.IsSuccess)
        {
            mApprovedFileConflictsByApprovalId.Remove(approvalRequest.Id);
        }

        await loadDataAsync();
    }

    private async Task<bool> tryShowApprovedFileConflictAsync(ApprovalRequestItem approvalRequest)
    {
        if (isFinalApprovalStep(approvalRequest) == false || string.IsNullOrWhiteSpace(approvalRequest.ApprovedFolderId))
        {
            return false;
        }

        DriveFileNameConflictCheckResult conflictCheckResult = await DriveFileNameConflictService.CheckFileNameConflictAsync(
            DriveFolderId.CreateOptional(approvalRequest.ApprovedFolderId),
            DriveItemName.CreateRequired(approvalRequest.FileName),
            CancellationToken.None);
        if (conflictCheckResult.HasConflict == false)
        {
            mApprovedFileConflictsByApprovalId.Remove(approvalRequest.Id);
            return false;
        }

        mApprovedFileConflictsByApprovalId[approvalRequest.Id] = conflictCheckResult;
        return true;
    }

    private Task rejectRequestAsync(ApprovalRequestItem approvalRequest)
    {
        return runApprovalOperationAsync(approvalRequest, () => executeRejectionAsync(approvalRequest));
    }

    private async Task executeRejectionAsync(ApprovalRequestItem approvalRequest)
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        ApprovalRequestId approvalRequestId = ApprovalRequestId.CreateExisting(approvalRequest.Id);
        ApprovalComment comment = ApprovalComment.CreateOptional(getCommentValue(approvalRequestId));
        ServiceResult rejectionResult = await ApprovalService.RejectRequestAsync(
            CurrentUserSessionOrNull,
            approvalRequestId,
            comment,
            CancellationToken.None);
        showOperationResult(rejectionResult, "반려되었습니다.");
        if (rejectionResult.IsSuccess)
        {
            await loadDataAsync();
        }
    }
}
