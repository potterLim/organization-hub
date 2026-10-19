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

    public IReadOnlyDictionary<int, int> ExpectedRevisions
    {
        get;
    }

    public NavigationMenuItemOrderUpdateRequest(IReadOnlyList<NavigationMenuItemId> orderedMenuItemIds, IReadOnlyDictionary<int, int>? expectedRevisionsOrNull = null)
    {
        ArgumentNullException.ThrowIfNull(orderedMenuItemIds);

        ExpectedRevisions = expectedRevisionsOrNull == null ? new Dictionary<int, int>() : new Dictionary<int, int>(expectedRevisionsOrNull);
        OrderedMenuItemIds = orderedMenuItemIds;
    }
}
