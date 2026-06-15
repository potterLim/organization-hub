using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class NavigationMenuItemOrderUpdateRequest
{
    public IReadOnlyList<NavigationMenuItemId> OrderedMenuItemIds
    {
        get;
    }

    public NavigationMenuItemOrderUpdateRequest(IReadOnlyList<NavigationMenuItemId> orderedMenuItemIds)
    {
        OrderedMenuItemIds = orderedMenuItemIds;
    }
}
