using System.Collections.Generic;

namespace OrganizationHub.Web.Data.Entities;

public class ApprovalWorkflow
{
    public int Revision
    {
        get; set;
    }
    public int Id
    {
        get; set;
    }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? AppliesToTeamId
    {
        get; set;
    }
    public Team? AppliesToTeam
    {
        get; set;
    }
    public int? AppliesToPositionId
    {
        get; set;
    }
    public OrganizationPosition? AppliesToPosition
    {
        get; set;
    }
    public int Priority
    {
        get; set;
    }
    public bool IsActive
    {
        get; set;
    }
    public List<ApprovalWorkflowStep> Steps { get; set; } = new List<ApprovalWorkflowStep>();
}
