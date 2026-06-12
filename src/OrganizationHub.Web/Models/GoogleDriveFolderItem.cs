using System;

namespace OrganizationHub.Web.Models;

public sealed class GoogleDriveFolderItem
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? ModifiedTime
    {
        get; set;
    }
}
