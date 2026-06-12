using System;

namespace OrganizationHub.Web.Data.Entities;

public class CalendarEventMetadata
{
    public int ID
    {
        get; set;
    }
    public string CalendarID { get; set; } = string.Empty;
    public string EventID { get; set; } = string.Empty;
    public int CreatedByUserID
    {
        get; set;
    }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt
    {
        get; set;
    }
}
