namespace OrganizationHub.Web.Models;

public sealed class GoogleHealthCheckItem
{
    public string Area { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
