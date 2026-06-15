using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalDefaultLineOption
{
    public ApprovalStepCount StepCount
    {
        get; set;
    }
    public int FirstApproverUserId
    {
        get; set;
    }
    public int SecondApproverUserId
    {
        get; set;
    }
    public int FinalApproverUserId
    {
        get; set;
    }
}
