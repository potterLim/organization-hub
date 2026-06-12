using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GoogleTaskListAssignmentOption
{
    public string TargetKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EGoogleTaskListAssignmentKind TaskListAssignmentKind
    {
        get; set;
    }
    public int? TeamID
    {
        get; set;
    }
}
