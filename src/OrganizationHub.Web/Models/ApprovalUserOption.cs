using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalUserOption
{
    public int UserID
    {
        get; set;
    }
    public string DisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public EUserRole Role
    {
        get; set;
    }
    public int? TeamID
    {
        get; set;
    }
    public string TeamName { get; set; } = string.Empty;

    public string GetDisplayLabel()
    {
        return DisplayName;
    }

    public string GetLabel()
    {
        if (string.IsNullOrWhiteSpace(TeamName))
        {
            return DisplayName + " · " + RoleName;
        }

        return DisplayName + " · " + RoleName + " · " + TeamName;
    }
}
