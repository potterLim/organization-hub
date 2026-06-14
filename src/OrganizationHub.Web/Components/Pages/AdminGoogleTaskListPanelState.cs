using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminGoogleTaskListPanelState
{
    public GoogleConnectionStatus Status
    {
        get; set;
    } = new GoogleConnectionStatus();

    public IReadOnlyList<GoogleTaskListAssignmentOption> TaskListAssignmentOptions
    {
        get; set;
    } = new List<GoogleTaskListAssignmentOption>();

    public GoogleWorkspaceAssignmentTargetKey SelectedTaskListTargetKey
    {
        get; set;
    } = GoogleWorkspaceAssignmentTargetKey.Empty;

    public IReadOnlyList<GoogleTaskListItem> TaskLists
    {
        get; set;
    } = new List<GoogleTaskListItem>();

    public bool IsEnsuringStandardTaskLists
    {
        get; set;
    }
}
