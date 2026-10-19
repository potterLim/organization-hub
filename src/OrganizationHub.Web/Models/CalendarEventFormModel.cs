using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class CalendarEventFormModel
{
    public string ETag { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public CalendarEventId EventIdValue
    {
        get
        {
            return CalendarEventId.CreateOptional(Id);
        }
    }
    public string CalendarId { get; set; } = string.Empty;
    public CalendarId CalendarIdValue
    {
        get
        {
            return OrganizationHub.Web.Domain.CalendarId.CreateOptional(CalendarId);
        }
    }
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartLocal
    {
        get; set;
    }
    public DateTime EndLocal
    {
        get; set;
    }
    public bool IsAllDay
    {
        get; set;
    }
}
