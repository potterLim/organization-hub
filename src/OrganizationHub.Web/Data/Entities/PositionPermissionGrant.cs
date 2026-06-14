using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class PositionPermissionGrant
{
    public int Id
    {
        get; set;
    }
    public int PositionId
    {
        get; set;
    }
    public OrganizationPosition? Position
    {
        get; set;
    }
    public string PermissionKey { get; set; } = string.Empty;
    public EPermissionScopeType ScopeType
    {
        get; set;
    }
    public int? TargetTeamId
    {
        get; set;
    }
    public Team? TargetTeam
    {
        get; set;
    }
    public bool IsAllowed
    {
        get; set;
    }
}
