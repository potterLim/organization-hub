using System.Collections.Generic;

namespace OrganizationHub.Web.Data.Entities;

public class NavigationMenuSection
{
    public int Id
    {
        get; set;
    }

    public string SectionKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int SortOrder
    {
        get; set;
    }

    public List<NavigationMenuItem> MenuItems { get; set; } = new List<NavigationMenuItem>();
}
