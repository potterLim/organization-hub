namespace OrganizationHub.Web.Domain;

public static class NavigationMenuRetirementPolicy
{
    public static bool IsRetired(NavigationMenuKey menuKey, NavigationPath href)
    {
        switch (menuKey.GetOptionalValue())
        {
            case NavigationMenuKey.MINUTES:
            case NavigationMenuKey.ARCHIVE:
            case NavigationMenuKey.RULES:
            case NavigationMenuKey.ON_SITE:
            case NavigationMenuKey.LEADERS:
                return true;
            default:
                return isRetiredHref(href);
        }
    }

    private static bool isRetiredHref(NavigationPath href)
    {
        string navigationPath = href.GetRequiredValue();
        return navigationPath == "/rules" || navigationPath == "/on-site" || navigationPath == "/leaders";
    }
}
