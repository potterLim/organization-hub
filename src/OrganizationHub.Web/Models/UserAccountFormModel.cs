using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class UserAccountFormModel
{
    public int Id
    {
        get; set;
    }
    public UserAccountId UserAccountIdValue
    {
        get
        {
            return UserAccountId.CreateOptional(Id);
        }
    }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public EUserAccountKind AccountKind
    {
        get; set;
    } = EUserAccountKind.OrganizationMember;
    public bool IsSystemAdministrator
    {
        get
        {
            return AccountKind == EUserAccountKind.SystemAdministrator;
        }
    }
    public int? TeamId
    {
        get; set;
    }
    public OrganizationTeamId? TeamIdOrNull
    {
        get
        {
            return OrganizationTeamId.CreateExistingOrNull(TeamId);
        }
    }
    public int? PositionId
    {
        get; set;
    }
    public OrganizationPositionId? PositionIdOrNull
    {
        get
        {
            return OrganizationPositionId.CreateExistingOrNull(PositionId);
        }
    }
    public bool IsActive { get; set; } = true;
}
