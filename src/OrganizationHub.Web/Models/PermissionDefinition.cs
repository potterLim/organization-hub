namespace OrganizationHub.Web.Models;

public sealed class PermissionDefinition
{
    public string Key
    {
        get; private init;
    }
    public string Category
    {
        get; private init;
    }
    public string DisplayName
    {
        get; private init;
    }
    public string Description
    {
        get; private init;
    }

    public PermissionDefinition(string key, string category, string displayName, string description)
    {
        Key = key;
        Category = category;
        DisplayName = displayName;
        Description = description;
    }
}
