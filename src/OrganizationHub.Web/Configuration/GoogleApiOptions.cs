namespace OrganizationHub.Web.Configuration;

public sealed class GoogleApiOptions
{
    public string ApplicationName { get; set; } = "Organization Hub";
    public string CalendarTimeZone { get; set; } = "Asia/Seoul";
    public string OAuthClientID { get; set; } = string.Empty;
    public string OAuthClientSecret { get; set; } = string.Empty;
}
