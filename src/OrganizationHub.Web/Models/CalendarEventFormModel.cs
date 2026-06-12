using System;

namespace OrganizationHub.Web.Models;

public sealed class CalendarEventFormModel
{
    public string ID { get; set; } = string.Empty;
    public string CalendarID { get; set; } = string.Empty;
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
