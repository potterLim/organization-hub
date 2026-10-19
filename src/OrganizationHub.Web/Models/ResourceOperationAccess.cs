using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ResourceOperationAccess
{
    public bool CanUseAnyItem
    {
        get;
    }
    public bool CanUseOwnItems
    {
        get;
    }

    public ResourceOperationAccess(bool canUseAnyItem, bool canUseOwnItems)
    {
        CanUseAnyItem = canUseAnyItem;
        CanUseOwnItems = canUseOwnItems;
    }

    public bool CanUse(UserAccountId userId, int? createdByUserIdOrNull)
    {
        return CanUseAnyItem || (CanUseOwnItems && createdByUserIdOrNull == userId.GetRequiredValue());
    }
}
