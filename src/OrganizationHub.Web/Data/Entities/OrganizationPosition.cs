using System.Collections.Generic;

namespace OrganizationHub.Web.Data.Entities;

public class OrganizationPosition
{
    public int Id
    {
        get; set;
    }
    public int TeamId
    {
        get; set;
    }
    public Team? Team
    {
        get; set;
    }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder
    {
        get; set;
    }
    public bool IsActive
    {
        get; set;
    }
    public bool IsSystemAdministrator
    {
        get; set;
    }
    public List<UserAccount> UserAccounts { get; set; } = new List<UserAccount>();
    public List<PositionPermissionGrant> PermissionGrants { get; set; } = new List<PositionPermissionGrant>();
    public List<ApprovalWorkflowStep> ApprovalWorkflowSteps { get; set; } = new List<ApprovalWorkflowStep>();
}
