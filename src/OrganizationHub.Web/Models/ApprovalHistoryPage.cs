using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalHistoryPage
{
    public required IReadOnlyList<ApprovalRequestItem> Items { get; init; }
    public required int PageNumber { get; init; }
    public required int TotalPages { get; init; }
    public required int TotalCount { get; init; }
    public required int MyOngoingCount { get; init; }
}
