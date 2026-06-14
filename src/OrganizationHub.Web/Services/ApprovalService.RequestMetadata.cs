using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private EUserRole getRequesterRole(ApprovalRequest request)
    {
        if (request.RequesterUser != null)
        {
            return request.RequesterUser.Role;
        }

        return EUserRole.TeamUser;
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
