namespace OrganizationHub.Web.Domain;

public static class NavigationMenuRetirementPolicy
{
    public static bool IsRetired(string menuKey, string href)
    {
        switch (menuKey)
        {
            case "Minutes":
            case "Archive":
            case "Rules":
            case "OnSite":
            case "Leaders":
                return true;
            default:
                return isRetiredHref(href);
        }
    }

    private static bool isRetiredHref(string href)
    {
        return href == "/rules" || href == "/on-site" || href == "/leaders";
    }
}
