namespace OrganizationHub.Web.Domain;

public sealed class PermissionDefinition
{
    public required string Key
    {
        get; init;
    }

    public required string Category
    {
        get; init;
    }

    public required string DisplayName
    {
        get; init;
    }

    public required string Description
    {
        get; init;
    }
}
