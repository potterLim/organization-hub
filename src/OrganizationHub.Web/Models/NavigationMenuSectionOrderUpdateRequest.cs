using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class NavigationMenuSectionOrderUpdateRequest
{
    public IReadOnlyList<NavigationMenuSectionId> OrderedSectionIds
    {
        get;
    }

    public NavigationMenuSectionOrderUpdateRequest(IReadOnlyList<NavigationMenuSectionId> orderedSectionIds)
    {
        ArgumentNullException.ThrowIfNull(orderedSectionIds);

        OrderedSectionIds = orderedSectionIds;
    }
}
