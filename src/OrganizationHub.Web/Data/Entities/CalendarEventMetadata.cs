using System;

namespace OrganizationHub.Web.Data.Entities;

public class CalendarEventMetadata
{
    public int Id
    {
        get; set;
    }
    public string CalendarId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public int CreatedByUserId
    {
        get; set;
    }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt
    {
        get; set;
    }
}
