using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalWorkflowFormModel
{
    public int ID
    {
        get; set;
    }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? AppliesToTeamID
    {
        get; set;
    }
    public int Priority
    {
        get; set;
    }
    public bool IsActive { get; set; } = true;
    public List<ApprovalWorkflowStepFormModel> Steps { get; set; } = new List<ApprovalWorkflowStepFormModel>();
}
