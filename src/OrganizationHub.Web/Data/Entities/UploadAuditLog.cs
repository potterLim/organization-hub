using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class UploadAuditLog
{
    public int Id
    {
        get; set;
    }
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public string FolderDisplayName { get; set; } = string.Empty;
    public EDriveFolderKind FolderKind
    {
        get; set;
    }
    public int UploaderUserId
    {
        get; set;
    }
    public string UploaderDisplayName { get; set; } = string.Empty;
    public EStoredCompatibilityRole UploaderStoredCompatibilityRole
    {
        get; set;
    }
    public int? UploaderTeamId
    {
        get; set;
    }
    public string UploaderTeamName { get; set; } = string.Empty;
    public int? UploaderPositionId
    {
        get; set;
    }
    public string UploaderPositionName { get; set; } = string.Empty;
    public int? TeamId
    {
        get; set;
    }
    public string TeamName { get; set; } = string.Empty;
    public DateTime UploadedAt
    {
        get; set;
    }
}
