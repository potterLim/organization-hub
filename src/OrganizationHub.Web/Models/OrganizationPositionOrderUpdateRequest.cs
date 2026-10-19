using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class OrganizationPositionOrderUpdateRequest
{
    public OrganizationTeamId TeamId
    {
        get;
    }

    public IReadOnlyList<OrganizationPositionId> OrderedPositionIds
    {
        get;
    }

    public IReadOnlyDictionary<int, int> ExpectedRevisions
    {
        get;
    }

    public OrganizationPositionOrderUpdateRequest(
        OrganizationTeamId teamId,
        IReadOnlyList<OrganizationPositionId> orderedPositionIds,
        IReadOnlyDictionary<int, int>? expectedRevisionsOrNull = null
    )
    {
        ArgumentNullException.ThrowIfNull(orderedPositionIds);

        TeamId = teamId;
        ExpectedRevisions = expectedRevisionsOrNull == null ? new Dictionary<int, int>() : new Dictionary<int, int>(expectedRevisionsOrNull);
        OrderedPositionIds = orderedPositionIds;
    }
}
