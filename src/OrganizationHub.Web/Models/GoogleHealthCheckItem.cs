using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GoogleHealthCheckItem
{
    public string Area { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public EGoogleHealthStatus Status
    {
        get; set;
    }
    public string Message { get; set; } = string.Empty;
}
