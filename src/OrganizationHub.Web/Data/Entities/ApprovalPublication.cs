using System;

namespace OrganizationHub.Web.Data.Entities;

public sealed class ApprovalPublication
{
    public int AttemptCount
    {
        get; set;
    }
    public DateTime? LastAttemptAt
    {
        get; set;
    }
    public string LastError { get; set; } = string.Empty;
    public int ApprovalRequestId
    {
        get; set;
    }
    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    public string TargetFileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public bool IsReplacement
    {
        get; set;
    }
    public bool IsCompleted
    {
        get; set;
    }
    public string LeaseToken { get; set; } = string.Empty;
    public DateTime? LeaseExpiresAt
    {
        get; set;
    }
    public DateTime NextAttemptAt
    {
        get; set;
    }
}
