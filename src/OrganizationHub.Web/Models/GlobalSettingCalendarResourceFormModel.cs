using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GlobalSettingCalendarResourceFormModel
{
    public string OrganizationCalendarId { get; set; } = string.Empty;
    public CalendarId OrganizationCalendarIdValue
    {
        get
        {
            return CalendarId.CreateOptional(OrganizationCalendarId);
        }
    }
    public string ResponsibleCalendarId { get; set; } = string.Empty;
    public CalendarId ResponsibleCalendarIdValue
    {
        get
        {
            return CalendarId.CreateOptional(ResponsibleCalendarId);
        }
    }
}
