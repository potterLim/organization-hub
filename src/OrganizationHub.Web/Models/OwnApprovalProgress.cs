namespace OrganizationHub.Web.Models;

public sealed class OwnApprovalProgress
{
    public required int PendingCount
    {
        get; init;
    }
    public required int PublishingCount
    {
        get; init;
    }
}
