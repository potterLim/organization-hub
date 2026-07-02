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

    public OrganizationTeamOrderUpdateRequest(IReadOnlyList<OrganizationTeamId> orderedTeamIds)
    {
        ArgumentNullException.ThrowIfNull(orderedTeamIds);

        OrderedTeamIds = orderedTeamIds;
    }
}
