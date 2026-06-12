using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GoogleFolderAssignmentOption
{
    public string TargetKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EGoogleFolderAssignmentKind FolderAssignmentKind
    {
        get; set;
    }
    public int? TeamID
    {
        get; set;
    }
}
