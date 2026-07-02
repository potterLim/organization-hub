using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class NavigationMenuItemOrderUpdateRequest
{
    public IReadOnlyList<NavigationMenuItemId> OrderedMenuItemIds
    {
        get;
    }

    public NavigationMenuItemOrderUpdateRequest(IReadOnlyList<NavigationMenuItemId> orderedMenuItemIds)
    {
        ArgumentNullException.ThrowIfNull(orderedMenuItemIds);

        OrderedMenuItemIds = orderedMenuItemIds;
    }
}
