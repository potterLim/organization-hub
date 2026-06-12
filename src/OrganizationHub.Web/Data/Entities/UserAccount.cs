using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class UserAccount
{
    public int ID
    {
        get; set;
    }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public EUserRole Role
    {
        get; set;
    }
    public int? TeamID
    {
        get; set;
    }
    public Team? Team
    {
        get; set;
    }
    public int? PositionID
    {
        get; set;
    }
    public OrganizationPosition? Position
    {
        get; set;
    }
    public bool IsActive
    {
        get; set;
    }
    public DateTime CreatedAt
    {
        get; set;
    }
    public DateTime? LastLoginAt
    {
        get; set;
    }
}

