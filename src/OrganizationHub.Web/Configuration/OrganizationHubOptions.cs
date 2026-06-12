namespace OrganizationHub.Web.Configuration;

public sealed class OrganizationHubOptions
{
    public string ApplicationName { get; set; } = "Organization Hub";
    public long MaximumUploadBytes { get; set; } = 20971520;
    public string SeedUsername { get; set; } = string.Empty;
    public string SeedPassword { get; set; } = string.Empty;
    public string SeedDisplayName { get; set; } = "Organization Hub Admin";
}

