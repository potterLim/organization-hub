using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalWorkflowStepFormModel
{
    public int ID
    {
        get; set;
    }
    public int StepOrder
    {
        get; set;
    }
    public string StepName { get; set; } = string.Empty;
    public EApproverSelectionType ApproverSelectionType { get; set; } = EApproverSelectionType.PositionInRequesterTeam;
    public int? ApproverUserID
    {
        get; set;
    }
    public int? ApproverPositionID
    {
        get; set;
    }
    public int? ApproverTeamID
    {
        get; set;
    }
    public string RequiredPermissionKey { get; set; } = string.Empty;
    public EPermissionScopeType ScopeType { get; set; } = EPermissionScopeType.OwnTeam;
    public bool IsRequired { get; set; } = true;
}
