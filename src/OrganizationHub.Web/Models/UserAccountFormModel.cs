using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class UserAccountFormModel
{
    public int ID
    {
        get; set;
    }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public EUserRole Role
    {
        get; set;
    }
    public int? TeamID
    {
        get; set;
    }
    public int? PositionID
    {
        get; set;
    }
    public bool IsActive { get; set; } = true;
}

