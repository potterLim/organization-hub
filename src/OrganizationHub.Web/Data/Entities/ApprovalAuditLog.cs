using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class ApprovalAuditLog
{
    public int ID
    {
        get; set;
    }
    public int ApprovalRequestID
    {
        get; set;
    }
    public ApprovalRequest? ApprovalRequest
    {
        get; set;
    }
    public int ActorUserID
    {
        get; set;
    }
    public string ActorDisplayName { get; set; } = string.Empty;
    public EApprovalAuditAction Action
    {
        get; set;
    }
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
