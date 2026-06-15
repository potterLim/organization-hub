using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private async Task approveRequestAsync(ApprovalRequestItem approvalRequest)
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        int nextApproverUserId = canChangeNextApprover(approvalRequest) ? getNextApproverValue(approvalRequest.Id) : 0;
        ApprovalRequestId approvalRequestId = ApprovalRequestId.CreateExisting(approvalRequest.Id);
        ApprovalUserId nextApproverId = ApprovalUserId.CreateOptional(nextApproverUserId);
        string comment = getCommentValue(approvalRequest.Id);
        ServiceResult approvalResult = await ApprovalService.ApproveRequestAsync(
            CurrentUserSessionOrNull,
            approvalRequestId,
            nextApproverId,
            comment,
            CancellationToken.None);
        showOperationResult(approvalResult, "승인되었습니다.");
        if (approvalResult.IsSuccess)
        {
            await loadDataAsync();
        }
    }

    private async Task rejectRequestAsync(ApprovalRequestItem approvalRequest)
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        ApprovalRequestId approvalRequestId = ApprovalRequestId.CreateExisting(approvalRequest.Id);
        string comment = getCommentValue(approvalRequest.Id);
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
