using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class DriveFileMetadata
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public IReadOnlyList<string> ParentFolderIDs { get; set; } = new List<string>();
}

