namespace OrganizationHub.Web.Models;

public sealed class GoogleCalendarListItem
{
    public string ID { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AccessRole { get; set; } = string.Empty;
    public bool IsPrimary
    {
        get; set;
    }
}
