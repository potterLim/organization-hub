using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private void ensureLocalStateForApprovalRequest(ApprovalRequestItem approvalRequest)
    {
        ApprovalRequestId approvalRequestId = ApprovalRequestId.CreateExisting(approvalRequest.Id);
        if (mCommentsByApprovalId.ContainsKey(approvalRequestId.GetRequiredValue()) == false)
        {
            setCommentValue(approvalRequestId, string.Empty);
        }
    }

    private string getCommentValue(ApprovalRequestId approvalRequestId)
    {
        string? commentOrNull;
        if (mCommentsByApprovalId.TryGetValue(approvalRequestId.GetRequiredValue(), out commentOrNull))
        {
            return commentOrNull;
        }

        return string.Empty;
    }

    private void setCommentValue(ApprovalRequestId approvalRequestId, string comment)
    {
        mCommentsByApprovalId[approvalRequestId.GetRequiredValue()] = comment;
    }
}
