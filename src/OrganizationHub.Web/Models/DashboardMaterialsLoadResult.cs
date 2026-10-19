using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

internal sealed class DashboardMaterialsLoadResult
{
    public required bool IsTimedOut
    {
        get; init;
    }
    public required IReadOnlyList<DriveFileItem> Files
    {
        get; init;
    }
}
