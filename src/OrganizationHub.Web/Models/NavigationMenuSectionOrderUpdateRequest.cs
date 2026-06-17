using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class NavigationMenuSectionOrderUpdateRequest
{
    public IReadOnlyList<NavigationMenuSectionId> OrderedSectionIds
    {
        get;
    }

    public NavigationMenuSectionOrderUpdateRequest(IReadOnlyList<NavigationMenuSectionId> orderedSectionIds)
    {
        OrderedSectionIds = orderedSectionIds;
    }
}
