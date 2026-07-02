namespace OrganizationHub.Web.Domain;

public static class OrganizationHubFileLimits
{
    public const int BYTES_PER_KILOBYTE = 1024;
    public const int BYTES_PER_MEGABYTE = BYTES_PER_KILOBYTE * BYTES_PER_KILOBYTE;
    public const int DEFAULT_MAXIMUM_UPLOAD_BYTES = 20 * BYTES_PER_MEGABYTE;
}
