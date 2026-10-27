using System;

namespace OrganizationHub.Web.Data.Entities;

public sealed class PendingUploadAudit
{
    public int Id
    {
        get; set;
    }
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt
    {
        get; set;
    }
}
