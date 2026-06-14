namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalUserID(int Value)
{
    public bool HasValue
    {
        get
        {
            return Value > 0;
        }
    }

    public static ApprovalUserID Empty
    {
        get
        {
            return new ApprovalUserID(0);
        }
    }
}
