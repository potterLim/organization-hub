namespace OrganizationHub.Web.Data.Entities;

public class NavigationMenuTeamPermission
{
    public int NavigationMenuItemId
    {
        get; set;
    }
    public NavigationMenuItem? NavigationMenuItem
    {
        get; set;
    }
    public int TeamId
    {
        get; set;
    }
    public Team? Team
    {
        get; set;
    }
}
