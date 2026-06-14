using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminGoogleCalendarPanelState
{
    public GoogleConnectionStatus Status
    {
        get; set;
    } = new GoogleConnectionStatus();

    public IReadOnlyList<GoogleCalendarAssignmentOption> CalendarAssignmentOptions
    {
        get; set;
    } = new List<GoogleCalendarAssignmentOption>();

    public GoogleWorkspaceAssignmentTargetKey SelectedCalendarTargetKey
    {
        get; set;
    } = GoogleWorkspaceAssignmentTargetKey.Empty;

    public IReadOnlyList<GoogleCalendarListItem> Calendars
    {
        get; set;
    } = new List<GoogleCalendarListItem>();

    public bool IsEnsuringStandardCalendars
    {
        get; set;
    }

    public Func<GoogleCalendarListItem, string> GetCalendarAccessDisplayName
    {
        get; set;
    } = getDefaultCalendarAccessDisplayName;

    private static string getDefaultCalendarAccessDisplayName(GoogleCalendarListItem calendar)
    {
        return string.Empty;
    }
}
