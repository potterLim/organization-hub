using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalWorkflowFormModel
{
    public int Id
    {
        get; set;
    }
    public ApprovalWorkflowId WorkflowIdValue
    {
        get
        {
            return ApprovalWorkflowId.CreateOptional(Id);
        }
    }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? AppliesToTeamId
    {
        get; set;
    }
    public OrganizationTeamId? AppliesToTeamIdOrNull
    {
        get
        {
            return OrganizationTeamId.CreateExistingOrNull(AppliesToTeamId);
        }
    }
    public int? AppliesToPositionId
    {
        get; set;
    }
    public OrganizationPositionId? AppliesToPositionIdOrNull
    {
        get
        {
            return OrganizationPositionId.CreateExistingOrNull(AppliesToPositionId);
        }
    }
    public int Priority
    {
        get; set;
    }
    public bool IsActive { get; set; } = true;
    public List<ApprovalWorkflowStepFormModel> Steps { get; set; } = new List<ApprovalWorkflowStepFormModel>();
}
