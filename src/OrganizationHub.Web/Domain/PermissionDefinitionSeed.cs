namespace OrganizationHub.Web.Domain;

internal sealed class PermissionDefinitionSeed
{
    public required PermissionKey Key
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
