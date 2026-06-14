using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private int getApproverSortOrder(UserAccount user, UserSession userSession)
    {
        return getApproverSortOrder(user.Role, userSession, OrganizationTeamId.CreateExistingOrNull(user.TeamId));
    }

    private int getApproverSortOrder(EUserRole role, UserSession userSession, OrganizationTeamId? teamIdOrNull)
    {
        if (UserRoleUtility.IsManagementMemberRole(userSession.Role))
        {
            switch (role)
            {
                case EUserRole.ManagementDirector:
                    return 0;
                case EUserRole.ManagementDeputyDirector:
                    return 1;
                case EUserRole.ManagementManager:
                    return 2;
                case EUserRole.TeamLeader:
                    return 3;
                case EUserRole.ManagementStaff:
                    return 4;
                case EUserRole.ManagementRecorder:
                    return 5;
                default:
                    return 100;
            }
        }

        OrganizationTeamId teamId = teamIdOrNull.GetValueOrDefault();
        bool isSameTeamLeader = userSession.HasTeam &&
            teamId.HasValue &&
            teamId.GetRequiredValue() == userSession.GetRequiredTeamIdValue() &&
            role == EUserRole.TeamLeader;
        if (isSameTeamLeader)
        {
            return 0;
        }

        if (role == EUserRole.TeamLeader)
        {
            return 1;
        }

        if (role == EUserRole.ManagementStaff)
        {
            return 2;
        }

        if (role == EUserRole.ManagementRecorder)
        {
            return 3;
        }

        return 4;
    }
}
