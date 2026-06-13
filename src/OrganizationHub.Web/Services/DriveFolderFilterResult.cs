using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class DriveFolderFilterResult
{
    public DriveFolderFilterResult(DriveFolderReference folder, EPermissionDecisionState decisionState)
    {
        Folder = folder;
        IsAllowed = decisionState == EPermissionDecisionState.Allowed;
    }

    public DriveFolderReference Folder
    {
        get; private init;
    }

    public bool IsAllowed
    {
        get; private init;
    }
}
