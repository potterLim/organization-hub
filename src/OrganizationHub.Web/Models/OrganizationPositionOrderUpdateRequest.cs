using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class OrganizationPositionOrderUpdateRequest
{
    public OrganizationTeamId TeamId
    {
        get;
    }

    public IReadOnlyList<OrganizationPositionId> OrderedPositionIds
    {
        get;
    }

    public OrganizationPositionOrderUpdateRequest(
        OrganizationTeamId teamId,
        IReadOnlyList<OrganizationPositionId> orderedPositionIds
    )
    {
        TeamId = teamId;
        OrderedPositionIds = orderedPositionIds;
    }
}
