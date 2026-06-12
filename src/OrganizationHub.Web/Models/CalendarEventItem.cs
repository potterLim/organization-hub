using System;

namespace OrganizationHub.Web.Models;

public sealed class CalendarEventItem
{
    public string ID { get; set; } = string.Empty;
    public string CalendarID { get; set; } = string.Empty;
    public string CalendarName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset? Start
    {
        get; set;
    }
    public DateTimeOffset? End
    {
        get; set;
    }
    public bool IsAllDay
    {
        get; set;
    }
    public bool CanEdit
    {
        get; set;
    }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime? CreatedAt
    {
        get; set;
    }
}
