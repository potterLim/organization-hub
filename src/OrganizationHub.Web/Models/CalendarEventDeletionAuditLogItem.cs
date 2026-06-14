using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class CalendarEventDeletionAuditLogItem
{
    public int Id
    {
        get; set;
    }
    public string CalendarId { get; set; } = string.Empty;
    public string CalendarName { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventSummary { get; set; } = string.Empty;
    public string EventLocation { get; set; } = string.Empty;
    public DateTimeOffset? EventStart
    {
        get; set;
    }
    public DateTimeOffset? EventEnd
    {
        get; set;
    }
    public bool IsAllDay
    {
        get; set;
    }
    public string DeletedByDisplayName { get; set; } = string.Empty;
    public EUserRole DeletedByRole
    {
        get; set;
    }
    public string DeletedByTeamName { get; set; } = string.Empty;
    public DateTime DeletedAt
    {
        get; set;
    }
    public ECalendarEventDeletionAuditStatus Status
    {
        get; set;
    }
    public string ErrorMessage { get; set; } = string.Empty;
}
