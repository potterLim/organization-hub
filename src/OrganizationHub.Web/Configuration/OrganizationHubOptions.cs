using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Configuration;

public sealed class OrganizationHubOptions
{
    public string ApplicationName { get; set; } = "Organization Hub";
    public long MaximumUploadBytes { get; set; } = OrganizationHubFileLimits.DEFAULT_MAXIMUM_UPLOAD_BYTES;
    public string SeedUsername { get; set; } = string.Empty;
    public string SeedPassword { get; set; } = string.Empty;
    public string SeedDisplayName { get; set; } = "Organization Hub 관리자";
}
