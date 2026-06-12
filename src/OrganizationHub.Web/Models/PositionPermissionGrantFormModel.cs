using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class PositionPermissionGrantFormModel
{
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsAllowed
    {
        get; set;
    }
    public EPermissionScopeType ScopeType { get; set; } = EPermissionScopeType.OwnTeam;
    public int? TargetTeamID
    {
        get; set;
    }
}
