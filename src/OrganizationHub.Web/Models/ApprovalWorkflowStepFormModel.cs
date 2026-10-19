using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalWorkflowStepFormModel
{
    private string mStepName = string.Empty;
    private bool mHasCustomStepName;
    public int Id
    {
        get; set;
    }
    public int StepOrder
    {
        get; set;
    }
    public string StepName
    {
        get
        {
            return mStepName;
        }
        set
        {
            mStepName = value;
            mHasCustomStepName = string.IsNullOrWhiteSpace(value) == false;
        }
    }
    public EApproverSelectionType ApproverSelectionType { get; set; } = EApproverSelectionType.PositionInRequesterTeam;
    public int? ApproverUserId
    {
        get; set;
    }
    public ApprovalUserId? ApproverUserIdOrNull
    {
        get
        {
            return ApprovalUserId.CreateExistingOrNull(ApproverUserId);
        }
    }
    public int? ApproverPositionId
    {
        get; set;
    }
    public OrganizationPositionId? ApproverPositionIdOrNull
    {
        get
        {
            return OrganizationPositionId.CreateExistingOrNull(ApproverPositionId);
        }
    }
    public int? ApproverTeamId
    {
        get; set;
    }
    public OrganizationTeamId? ApproverTeamIdOrNull
    {
        get
        {
            return OrganizationTeamId.CreateExistingOrNull(ApproverTeamId);
        }
    }
    public string RequiredPermissionKey { get; set; } = string.Empty;
    public EPermissionScopeType ScopeType { get; set; } = EPermissionScopeType.OwnTeam;
    public bool IsRequired { get; set; } = true;
    public void ApplyDefaultName(string name)
    {
        if (mHasCustomStepName == false)
        {
            mStepName = name;
        }
    }
}
