using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class CalendarEventDeletionAuditLog
{
    public int ID
    {
        get; set;
    }
    public string CalendarID { get; set; } = string.Empty;
    public string CalendarName { get; set; } = string.Empty;
    public string EventID { get; set; } = string.Empty;
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
    public int DeletedByUserID
    {
        get; set;
    }
    public string DeletedByDisplayName { get; set; } = string.Empty;
    public EUserRole DeletedByRole
    {
        get; set;
    }
    public int? DeletedByTeamID
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
