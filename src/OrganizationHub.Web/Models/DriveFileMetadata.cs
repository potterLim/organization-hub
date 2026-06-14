using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class DriveFileMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public IReadOnlyList<string> ParentFolderIds { get; set; } = new List<string>();
}

