using System.Threading;
using System.Threading.Tasks;
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

    private async Task approveRequestAsync(
        ApprovalRequestItem approvalRequest,
        EDriveFileNameConflictResolution? conflictResolutionOrNull)
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

    private async Task rejectRequestAsync(ApprovalRequestItem approvalRequest)
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
