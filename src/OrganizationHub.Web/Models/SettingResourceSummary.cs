namespace OrganizationHub.Web.Models;

public sealed class SettingResourceSummary
{
    public string GroupName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsConfigured
    {
        get; set;
    }
}
