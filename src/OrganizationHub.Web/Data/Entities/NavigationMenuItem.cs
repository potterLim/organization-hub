using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class NavigationMenuItem
{
    public int Id
    {
        get; set;
    }
    public string MenuKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string RequiredPermissionKeys { get; set; } = string.Empty;
    public int SectionId
    {
        get; set;
    }
    public NavigationMenuSection? Section
    {
        get; set;
    }
    public int SortOrder
    {
        get; set;
    }
    public EMenuAccessLevel AccessLevel
    {
        get; set;
    }
    public bool IsActive
    {
        get; set;
    }
    public bool IsVisibleToAllTeams
    {
        get; set;
    }
    public bool IsLocked
    {
        get; set;
    }
    public List<NavigationMenuTeamPermission> TeamPermissions { get; set; } = new List<NavigationMenuTeamPermission>();
}
