using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalApproverSelection
{
    public ApprovalUserId FirstApproverUserId
    {
        get;
    }

    public ApprovalUserId SecondApproverUserId
    {
        get;
    }

    public ApprovalUserId FinalApproverUserId
    {
        get;
    }

    public ApprovalApproverSelection(
        ApprovalUserId firstApproverUserId,
        ApprovalUserId secondApproverUserId,
        ApprovalUserId finalApproverUserId)
    {
        FirstApproverUserId = firstApproverUserId;
        SecondApproverUserId = secondApproverUserId;
        FinalApproverUserId = finalApproverUserId;
    }
}
