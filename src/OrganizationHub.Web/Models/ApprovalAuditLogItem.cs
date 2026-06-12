using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalAuditLogItem
{
    public EApprovalAuditAction Action
    {
        get; set;
    }
    public string ActorDisplayName { get; set; } = string.Empty;
    public int? StepNumber
    {
        get; set;
    }
    public string Comment { get; set; } = string.Empty;
    public string PreviousApproverDisplayName { get; set; } = string.Empty;
    public string NewApproverDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt
    {
        get; set;
    }
}
