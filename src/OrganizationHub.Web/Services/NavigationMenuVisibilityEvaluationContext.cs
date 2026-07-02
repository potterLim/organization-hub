using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class NavigationMenuVisibilityEvaluationContext
{
    public required UserSession UserSession
    {
        get; init;
    }

    public bool CanBypassMenuVisibility
    {
        get; init;
    }
}
