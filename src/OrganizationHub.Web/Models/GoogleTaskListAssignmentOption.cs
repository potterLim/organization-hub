using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GoogleTaskListAssignmentOption
{
    public GoogleWorkspaceAssignmentTargetKey TargetKey
    {
        get; set;
    }
    public string TargetSelectionValue
    {
        get
        {
            return TargetKey.GetSelectionValue();
        }
    }
    public string DisplayName
    {
        get; set;
    } = string.Empty;
    public EGoogleTaskListAssignmentKind TaskListAssignmentKind
    {
        get; set;
    }
    public OrganizationTeamId? TeamIdOrNull
    {
        get; set;
    }
}
