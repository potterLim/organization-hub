using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class PositionPermissionGrantFormModel
{
    public string PermissionKey { get; set; } = string.Empty;
    public PermissionKey PermissionKeyValue
    {
        get
        {
            return new PermissionKey(PermissionKey);
        }
    }
    public bool IsAllowed
    {
        get; set;
    }
    public EPermissionScopeType ScopeType { get; set; } = EPermissionScopeType.OwnTeam;
    public int? TargetTeamId
    {
        get; set;
    }
    public OrganizationTeamId? TargetTeamIdOrNull
    {
        get
        {
            return OrganizationTeamId.CreateExistingOrNull(TargetTeamId);
        }
    }
}
