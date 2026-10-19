using System;

namespace OrganizationHub.Web.Data.Entities;

public sealed class ApprovalDocument
{
    public int ApprovalRequestId
    {
        get; set;
    }
    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
