using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private void bumpApprovalVersion()
    {
        if (mChangeVersionServiceOrNull == null)
        {
            return;
        }

        mChangeVersionServiceOrNull.Bump(ChangeVersionKeys.APPROVAL);
    }
}
