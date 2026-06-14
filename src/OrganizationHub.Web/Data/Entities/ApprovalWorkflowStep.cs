using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class ApprovalWorkflowStep
{
    public int Id
    {
        get; set;
    }
    public int ApprovalWorkflowId
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
    public int? ApproverUserId
    {
        get; set;
    }
    public UserAccount? ApproverUser
    {
        get; set;
    }
    public int? ApproverPositionId
    {
        get; set;
    }
    public OrganizationPosition? ApproverPosition
    {
        get; set;
    }
    public int? ApproverTeamId
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
