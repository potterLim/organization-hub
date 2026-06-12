namespace OrganizationHub.Web.Data.Entities;

public class NavigationMenuTeamPermission
{
    public int NavigationMenuItemID
    {
        get; set;
    }
    public NavigationMenuItem? NavigationMenuItem
    {
        get; set;
    }
    public int TeamID
    {
        get; set;
    }
    public Team? Team
    {
        get; set;
    }
}
