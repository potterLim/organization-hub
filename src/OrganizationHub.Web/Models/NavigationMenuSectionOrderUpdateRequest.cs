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

    public IReadOnlyDictionary<int, int> ExpectedRevisions { get; }

    public NavigationMenuSectionOrderUpdateRequest(IReadOnlyList<NavigationMenuSectionId> orderedSectionIds, IReadOnlyDictionary<int, int>? expectedRevisionsOrNull = null)
    {
        ArgumentNullException.ThrowIfNull(orderedSectionIds);

        ExpectedRevisions = expectedRevisionsOrNull == null ? new Dictionary<int, int>() : new Dictionary<int, int>(expectedRevisionsOrNull);
        OrderedSectionIds = orderedSectionIds;
    }
}
