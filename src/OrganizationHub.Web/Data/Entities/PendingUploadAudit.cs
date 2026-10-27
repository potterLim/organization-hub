using System;

namespace OrganizationHub.Web.Data.Entities;

public sealed class PendingUploadAudit
{
    public int Id
    {
        get; set;
    }
    public string Payload { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public bool IsUploadConfirmed
    {
        get; set;
    }
    public DateTime? LastAttemptAt
    {
        get; set;
    }
    public DateTime CreatedAt
    {
        get; set;
    }
}
