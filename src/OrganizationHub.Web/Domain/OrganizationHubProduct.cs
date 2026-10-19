namespace OrganizationHub.Web.Domain;

public static class OrganizationHubProduct
{
    public const string NAME = "Organization Hub";

    public static string FormatPageTitle(string pageName)
    {
        return string.IsNullOrWhiteSpace(pageName) ? NAME : pageName.Trim() + " · " + NAME;
    }
}
