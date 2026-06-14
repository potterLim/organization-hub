namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalStepCount(int Value)
{
    public bool RequiresFirstApprover
    {
        get
        {
            return Value >= 2;
        }
    }

    public bool RequiresSecondApprover
    {
        get
        {
            return Value == 3;
        }
    }
}
