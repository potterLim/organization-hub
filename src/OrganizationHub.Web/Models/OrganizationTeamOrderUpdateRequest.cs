using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class OrganizationTeamOrderUpdateRequest
{
    public IReadOnlyList<OrganizationTeamId> OrderedTeamIds
    {
        get;
    }

    public OrganizationTeamOrderUpdateRequest(IReadOnlyList<OrganizationTeamId> orderedTeamIds)
    {
        OrderedTeamIds = orderedTeamIds;
    }
}
