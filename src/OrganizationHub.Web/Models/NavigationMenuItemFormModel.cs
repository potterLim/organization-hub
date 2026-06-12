using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class NavigationMenuItemFormModel
{
    public int ID
    {
        get; set;
    }
    public string DisplayName { get; set; } = string.Empty;
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
    public List<int> TeamIDs { get; set; } = new List<int>();
}
