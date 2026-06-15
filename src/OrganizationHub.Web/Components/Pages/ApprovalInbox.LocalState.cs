using System.Linq;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private void ensureLocalStateForApprovalRequest(ApprovalRequestItem approvalRequest)
    {
        if (mCommentsByApprovalId.ContainsKey(approvalRequest.Id) == false)
        {
            mCommentsByApprovalId[approvalRequest.Id] = string.Empty;
        }

        if (canChangeNextApprover(approvalRequest) && mNextApproverUserIdByApprovalId.ContainsKey(approvalRequest.Id) == false)
        {
            ApprovalStepItem? nextStepOrNull = approvalRequest.Steps.FirstOrDefault(step => step.StepNumber == approvalRequest.CurrentStepNumber + 1);
            if (nextStepOrNull != null)
            {
                mNextApproverUserIdByApprovalId[approvalRequest.Id] = nextStepOrNull.ApproverUserId;
            }
        }
    }

    private bool canChangeNextApprover(ApprovalRequestItem approvalRequest)
    {
        return approvalRequest.CurrentStepNumber + 1 < approvalRequest.StepCount;
    }

    private string getCommentValue(int approvalRequestId)
    {
        string? commentOrNull;
        if (mCommentsByApprovalId.TryGetValue(approvalRequestId, out commentOrNull))
        {
            return commentOrNull;
        }

        return string.Empty;
    }

    private void setCommentValue(int approvalRequestId, string comment)
    {
        mCommentsByApprovalId[approvalRequestId] = comment;
    }

    private int getNextApproverValue(int approvalRequestId)
    {
        int nextApproverUserId;
        if (mNextApproverUserIdByApprovalId.TryGetValue(approvalRequestId, out nextApproverUserId))
        {
            return nextApproverUserId;
        }

        return 0;
    }

    private void setNextApproverValue(int approvalRequestId, int nextApproverUserId)
    {
        mNextApproverUserIdByApprovalId[approvalRequestId] = nextApproverUserId;
    }
}
