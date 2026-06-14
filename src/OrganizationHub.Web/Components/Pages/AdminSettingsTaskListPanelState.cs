using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminSettingsTaskListPanelState
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

    public string SelectedTaskListAssignmentDisplayName
    {
        get; set;
    } = string.Empty;

    public string SelectedTaskListAssignmentDetail
    {
        get; set;
    } = string.Empty;

    public IReadOnlyList<GoogleTaskListItem> TaskLists
    {
        get; set;
    } = new List<GoogleTaskListItem>();
}
