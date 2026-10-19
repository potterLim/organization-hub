using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalPublicationPage
{
    public required IReadOnlyList<ApprovalPublicationItem> Items
    {
        get; init;
    }
    public required int TotalCount
    {
        get; init;
    }
    public required int PageNumber
    {
        get; init;
    }
    public required int TotalPages
    {
        get; init;
    }
}
