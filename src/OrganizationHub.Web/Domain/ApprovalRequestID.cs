namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalRequestID(int Value)
{
    public bool HasValue
    {
        get
        {
            return Value > 0;
        }
    }
}
