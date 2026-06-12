using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class UploadAuditLogItem
{
    public string FileID { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FolderDisplayName { get; set; } = string.Empty;
    public EDriveFolderKind FolderKind
    {
        get; set;
    }
    public string UploaderDisplayName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public DateTime UploadedAt
    {
        get; set;
    }
}
