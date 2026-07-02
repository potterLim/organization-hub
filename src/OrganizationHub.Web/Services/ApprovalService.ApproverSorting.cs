using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private const int SAME_TEAM_SORT_WEIGHT = 0;
    private const int DIFFERENT_TEAM_SORT_WEIGHT = 1000000;
    private const int MISSING_POSITION_SORT_WEIGHT = 900000;
    private const int POSITION_SORT_ORDER_MULTIPLIER = 1000;
    private const int MISSING_TEAM_SORT_WEIGHT = 900;

    private int getApproverSortOrder(UserAccount user, UserSession userSession)
    {
        int sameTeamWeight = getSameTeamSortWeight(user, userSession);
        int positionSortOrder = getPositionSortOrder(user);
        int teamSortOrder = getTeamSortOrder(user);
        return sameTeamWeight + positionSortOrder + teamSortOrder;
    }

    private int getSameTeamSortWeight(UserAccount user, UserSession userSession)
    {
        int? requesterTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(userSession.OrganizationTeamIdOrNull);
        if (requesterTeamIdValueOrNull.HasValue && user.TeamId == requesterTeamIdValueOrNull)
        {
            return SAME_TEAM_SORT_WEIGHT;
        }

        return DIFFERENT_TEAM_SORT_WEIGHT;
    }

    private int getPositionSortOrder(UserAccount user)
    {
        if (user.Position == null)
        {
            return MISSING_POSITION_SORT_WEIGHT;
        }

        return user.Position.SortOrder * POSITION_SORT_ORDER_MULTIPLIER;
    }

    private int getTeamSortOrder(UserAccount user)
    {
        if (user.Team == null)
        {
            return MISSING_TEAM_SORT_WEIGHT;
        }

        return user.Team.SortOrder;
    }
}
