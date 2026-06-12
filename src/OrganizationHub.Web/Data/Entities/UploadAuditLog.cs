using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class UploadAuditLog
{
    public int ID
    {
        get; set;
    }
    public string FileID { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FolderID { get; set; } = string.Empty;
    public string FolderDisplayName { get; set; } = string.Empty;
    public EDriveFolderKind FolderKind
    {
        get; set;
    }
    public int UploaderUserID
    {
        get; set;
    }
    public string UploaderDisplayName { get; set; } = string.Empty;
    public EUserRole UploaderRole
    {
        get; set;
    }
    public int? TeamID
    {
        get; set;
    }
    public string TeamName { get; set; } = string.Empty;
    public DateTime UploadedAt
    {
        get; set;
    }
}
