using System.Collections.Generic;

namespace OrganizationHub.Web.Data.Entities;

public class Team
{
    public int ID
    {
        get; set;
    }
    public string TeamKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive
    {
        get; set;
    }
    public int SortOrder
    {
        get; set;
    }
    public string? MaterialsFolderID
    {
        get; set;
    }
    public string? ApprovedFolderID
    {
        get; set;
    }
    public string? CalendarID
    {
        get; set;
    }
    public string? TaskListID
    {
        get; set;
    }
    public List<UserAccount> UserAccounts { get; set; } = new List<UserAccount>();
    public List<OrganizationPosition> Positions { get; set; } = new List<OrganizationPosition>();
    public List<NavigationMenuTeamPermission> NavigationMenuPermissions { get; set; } = new List<NavigationMenuTeamPermission>();
    public List<ApprovalRequest> ApprovalRequests { get; set; } = new List<ApprovalRequest>();
    public List<ApprovalWorkflow> ApprovalWorkflows { get; set; } = new List<ApprovalWorkflow>();
}
