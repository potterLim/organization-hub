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

    public ApprovalUserId FirstApproverUserIdValue
    {
        get
        {
            return ApprovalUserId.CreateOptional(FirstApproverUserId);
        }
        set
        {
            FirstApproverUserId = value.GetOptionalValue();
        }
    }

    public int SecondApproverUserId
    {
        get; set;
    }

    public ApprovalUserId SecondApproverUserIdValue
    {
        get
        {
            return ApprovalUserId.CreateOptional(SecondApproverUserId);
        }
        set
        {
            SecondApproverUserId = value.GetOptionalValue();
        }
    }

    public int FinalApproverUserId
    {
        get; set;
    }

    public ApprovalUserId FinalApproverUserIdValue
    {
        get
        {
            return ApprovalUserId.CreateOptional(FinalApproverUserId);
        }
        set
        {
            FinalApproverUserId = value.GetOptionalValue();
        }
    }
}
