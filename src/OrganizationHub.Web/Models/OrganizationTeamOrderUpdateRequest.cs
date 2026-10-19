using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class OrganizationTeamOrderUpdateRequest
{
    public IReadOnlyList<OrganizationTeamId> OrderedTeamIds
    {
        get;
    }

    public IReadOnlyDictionary<int, int> ExpectedRevisions { get; }

    public OrganizationTeamOrderUpdateRequest(IReadOnlyList<OrganizationTeamId> orderedTeamIds, IReadOnlyDictionary<int, int>? expectedRevisionsOrNull = null)
    {
        ArgumentNullException.ThrowIfNull(orderedTeamIds);

        ExpectedRevisions = expectedRevisionsOrNull == null ? new Dictionary<int, int>() : new Dictionary<int, int>(expectedRevisionsOrNull);
        OrderedTeamIds = orderedTeamIds;
    }
}
