using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class ApprovalWorkflowStep
{
    public int ID
    {
        get; set;
    }
    public int ApprovalWorkflowID
    {
        get; set;
    }
    public ApprovalWorkflow? ApprovalWorkflow
    {
        get; set;
    }
    public int StepOrder
    {
        get; set;
    }
    public string StepName { get; set; } = string.Empty;
    public EApproverSelectionType ApproverSelectionType
    {
        get; set;
    }
    public int? ApproverUserID
    {
        get; set;
    }
    public UserAccount? ApproverUser
    {
        get; set;
    }
    public int? ApproverPositionID
    {
        get; set;
    }
    public OrganizationPosition? ApproverPosition
    {
        get; set;
    }
    public int? ApproverTeamID
    {
        get; set;
    }
    public Team? ApproverTeam
    {
        get; set;
    }
    public string RequiredPermissionKey { get; set; } = string.Empty;
    public EPermissionScopeType ScopeType
    {
        get; set;
    }
    public bool IsRequired
    {
        get; set;
    }
}
