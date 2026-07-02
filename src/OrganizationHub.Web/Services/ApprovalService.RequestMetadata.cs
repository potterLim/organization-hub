using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private EStoredCompatibilityRole getRequesterRole(ApprovalRequest request)
    {
        if (request.RequesterUser != null)
        {
            return request.RequesterUser.StoredCompatibilityRole;
        }

        return EStoredCompatibilityRole.TeamUser;
    }

    private int? getRequesterPositionIdOrNull(ApprovalRequest request)
    {
        if (request.RequesterPositionId.HasValue)
        {
            return request.RequesterPositionId.GetValueOrDefault();
        }

        if (request.RequesterUser == null)
        {
            return null;
        }

        return request.RequesterUser.PositionId;
    }

    private string getRequesterPositionName(ApprovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequesterPositionName) == false)
        {
            return request.RequesterPositionName.Trim();
        }

        if (request.RequesterUser == null || request.RequesterUser.Position == null)
        {
            return string.Empty;
        }

        return request.RequesterUser.Position.Name;
    }

    private int? getRequesterTeamIdOrNull(ApprovalRequest request)
    {
        if (request.RequesterTeamId.HasValue)
        {
            return request.RequesterTeamId.GetValueOrDefault();
        }

        if (request.RequesterUser == null)
        {
            return request.TeamId;
        }

        return request.RequesterUser.TeamId;
    }

    private string getRequesterTeamName(ApprovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequesterTeamName) == false)
        {
            return request.RequesterTeamName.Trim();
        }

        if (request.RequesterUser != null && request.RequesterUser.Team != null)
        {
            return request.RequesterUser.Team.DisplayName;
        }

        if (string.IsNullOrWhiteSpace(request.TeamName))
        {
            return string.Empty;
        }

        return request.TeamName.Trim();
    }

    private void bumpApprovalVersion()
    {
        if (mChangeVersionServiceOrNull == null)
        {
            return;
        }

        mChangeVersionServiceOrNull.Bump(ChangeVersionKeys.APPROVAL);
    }
}
