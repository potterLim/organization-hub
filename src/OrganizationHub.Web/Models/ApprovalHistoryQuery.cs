using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalHistoryQuery
{
    public required int PageNumber { get; init; }
    public required string Keyword { get; init; }
    public required EApprovalHistoryFilter Filter { get; init; }
    public DateTime? RequestedFromUtcOrNull { get; init; }
    public DateTime? RequestedToUtcExclusiveOrNull { get; init; }
}
