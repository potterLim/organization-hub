using System;

namespace OrganizationHub.Web.Models;

public sealed class DriveFileItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public DateTime? CreatedTime
    {
        get; set;
    }
    public DateTime? ModifiedTime
    {
        get; set;
    }
    public string IconLink { get; set; } = string.Empty;
    public string ThumbnailLink { get; set; } = string.Empty;
    public bool IsFolder
    {
        get; set;
    }
    public string TeamKey { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string FolderDisplayName { get; set; } = string.Empty;
    public string UploadedByDisplayName { get; set; } = string.Empty;
    public DateTime? UploadedAt
    {
        get; set;
    }
    public bool CanPreviewInSite
    {
        get; set;
    }
    public long? Size
    {
        get; set;
    }
}
