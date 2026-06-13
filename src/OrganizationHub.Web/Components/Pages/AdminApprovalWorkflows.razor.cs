using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
    private List<Team> mTeams = new List<Team>();
    private List<OrganizationPosition> mPositions = new List<OrganizationPosition>();
    private List<UserAccount> mUsers = new List<UserAccount>();
    private List<PermissionDefinition> mPermissionDefinitions = new List<PermissionDefinition>();
    private List<ApprovalWorkflow> mWorkflows = new List<ApprovalWorkflow>();
    private ApprovalWorkflowFormModel mFormModel = new ApprovalWorkflowFormModel();
    private string mFormTitle = "새 결재 경로";
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadDataAsync();
        clearForm();
    }

    private async Task loadDataAsync()
    {
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        IReadOnlyList<OrganizationPosition> positions = await PositionManagementService.GetPositionsAsync(CancellationToken.None);
        IReadOnlyList<UserAccount> users = await ApprovalWorkflowManagementService.GetApproverUsersAsync(CancellationToken.None);
        IReadOnlyList<PermissionDefinition> permissionDefinitions = await PositionManagementService.GetPermissionDefinitionsAsync(CancellationToken.None);
        IReadOnlyList<ApprovalWorkflow> workflows = await ApprovalWorkflowManagementService.GetWorkflowsAsync(CancellationToken.None);
        mTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
        mPositions = positions.Where(position => position.IsActive).ToList();
        mUsers = users.ToList();
        mPermissionDefinitions = permissionDefinitions.OrderBy(definition => definition.Category).ThenBy(definition => definition.DisplayName).ToList();
        mWorkflows = workflows.ToList();
    }

    private async Task saveWorkflowAsync()
    {
        ServiceResult result = mFormModel.ID == 0 ? await ApprovalWorkflowManagementService.CreateWorkflowAsync(mFormModel, CancellationToken.None) : await ApprovalWorkflowManagementService.UpdateWorkflowAsync(mFormModel, CancellationToken.None);
        showResult(result, "결재 경로 설정이 저장되었습니다.");
        if (result.IsSuccess)
        {
            await loadDataAsync();
            clearForm();
        }
    }

    private void editWorkflow(ApprovalWorkflow workflow)
    {
        mFormModel = new ApprovalWorkflowFormModel();
        mFormModel.ID = workflow.ID;
        mFormModel.Name = workflow.Name;
        mFormModel.Description = workflow.Description;
        mFormModel.AppliesToTeamID = workflow.AppliesToTeamID;
        mFormModel.Priority = workflow.Priority;
        mFormModel.IsActive = workflow.IsActive;
        foreach (ApprovalWorkflowStep step in workflow.Steps.OrderBy(step => step.StepOrder))
        {
            ApprovalWorkflowStepFormModel stepFormModel = new ApprovalWorkflowStepFormModel();
            stepFormModel.ID = step.ID;
            stepFormModel.StepOrder = step.StepOrder;
            stepFormModel.StepName = step.StepName;
            stepFormModel.ApproverSelectionType = step.ApproverSelectionType;
            stepFormModel.ApproverUserID = step.ApproverUserID;
            stepFormModel.ApproverPositionID = step.ApproverPositionID;
            stepFormModel.ApproverTeamID = step.ApproverTeamID;
            stepFormModel.RequiredPermissionKey = step.RequiredPermissionKey;
            stepFormModel.ScopeType = step.ScopeType;
            stepFormModel.IsRequired = step.IsRequired;
            mFormModel.Steps.Add(stepFormModel);
        }

        mFormTitle = "결재 경로 수정";
    }

    private async Task toggleWorkflowActiveAsync(ApprovalWorkflow workflow)
    {
        ServiceResult result = workflow.IsActive ? await ApprovalWorkflowManagementService.DeactivateWorkflowAsync(workflow.ID, CancellationToken.None) : await ApprovalWorkflowManagementService.ActivateWorkflowAsync(workflow.ID, CancellationToken.None);
        showResult(result, "결재 경로 상태가 변경되었습니다.");
        await loadDataAsync();
    }

    private void clearForm()
    {
        mFormModel = new ApprovalWorkflowFormModel();
        mFormModel.Priority = 100;
        mFormModel.IsActive = true;
        addStep();
        mFormTitle = "새 결재 경로";
    }

    private void addStep()
    {
        int stepOrder = mFormModel.Steps.Count + 1;
        ApprovalWorkflowStepFormModel step = new ApprovalWorkflowStepFormModel();
        step.StepOrder = stepOrder;
        step.StepName = getDefaultStepName(stepOrder);
        step.IsRequired = true;
        mFormModel.Steps.Add(step);
    }

    private void removeStep(ApprovalWorkflowStepFormModel step)
    {
        if (mFormModel.Steps.Count <= 1)
        {
            return;
        }

        mFormModel.Steps.Remove(step);
        renumberSteps();
    }

    private void renumberSteps()
    {
        int stepOrder = 1;
        foreach (ApprovalWorkflowStepFormModel step in mFormModel.Steps.OrderBy(step => step.StepOrder))
        {
            step.StepOrder = stepOrder;
            stepOrder++;
        }
    }

    private void setAppliesToTeamID(int? teamIDOrNull)
    {
        mFormModel.AppliesToTeamID = teamIDOrNull;
    }

    private void setApproverSelectionType(ApprovalWorkflowStepFormModel step, EApproverSelectionType selectionType)
    {
        step.ApproverSelectionType = selectionType;
        step.ApproverUserID = null;
        step.ApproverTeamID = null;
        step.RequiredPermissionKey = string.Empty;
        if (usesPosition(selectionType) == false)
        {
            step.ApproverPositionID = null;
        }
    }

    private bool usesPosition(EApproverSelectionType selectionType)
    {
        return selectionType == EApproverSelectionType.SpecificPosition ||
            selectionType == EApproverSelectionType.PositionInRequesterTeam ||
            selectionType == EApproverSelectionType.PositionInSelectedTeam;
    }

    private string getWorkflowTeamName(ApprovalWorkflow workflow)
    {
        if (workflow.AppliesToTeam == null)
        {
            return "전체";
        }

        return workflow.AppliesToTeam.DisplayName;
    }

    private string getPositionDisplayName(OrganizationPosition position)
    {
        if (position.Team == null)
        {
            return position.Name;
        }

        return position.Team.DisplayName + " / " + position.Name;
    }

    private string getUserDisplayName(UserAccount user)
    {
        if (user.Team == null)
        {
            return user.DisplayName;
        }

        return user.DisplayName + " · " + user.Team.DisplayName;
    }

    private string getWorkflowTargetPreview()
    {
        if (mFormModel.AppliesToTeamID.HasValue == false)
        {
            return "모든 팀 요청에 적용됩니다. 적용 우선순위가 낮은 경로를 먼저 사용합니다.";
        }

        Team? teamOrNull = mTeams.FirstOrDefault(team => team.ID == mFormModel.AppliesToTeamID.Value);
        if (teamOrNull == null)
        {
            return "선택한 팀 요청에 적용됩니다.";
        }

        return teamOrNull.DisplayName + " 팀 요청에 적용됩니다.";
    }

    private string getWorkflowStepPreview(ApprovalWorkflowStepFormModel step)
    {
        string approverText = getApproverPreview(step);
        if (step.IsRequired)
        {
            return approverText + "에게 반드시 결재를 받습니다.";
        }

        return approverText + "에게 필요한 경우 결재를 받습니다.";
    }

    private string getApproverPreview(ApprovalWorkflowStepFormModel step)
    {
        if (step.ApproverSelectionType == EApproverSelectionType.SpecificUser)
        {
            UserAccount? userOrNull = mUsers.FirstOrDefault(user => step.ApproverUserID.HasValue && user.ID == step.ApproverUserID.Value);
            if (userOrNull == null)
            {
                return "선택한 사용자";
            }

            return getUserDisplayName(userOrNull);
        }

        if (step.ApproverSelectionType == EApproverSelectionType.SpecificPosition)
        {
            return getSelectedPositionPreview(step, "선택한 직책");
        }

        if (step.ApproverSelectionType == EApproverSelectionType.PositionInRequesterTeam)
        {
            return "요청자 팀의 " + getSelectedPositionPreview(step, "선택한 직책");
        }

        if (step.ApproverSelectionType == EApproverSelectionType.PositionInSelectedTeam)
        {
            string teamText = "선택한 팀";
            Team? teamOrNull = mTeams.FirstOrDefault(team => step.ApproverTeamID.HasValue && team.ID == step.ApproverTeamID.Value);
            if (teamOrNull != null)
            {
                teamText = teamOrNull.DisplayName;
            }

            return teamText + "의 " + getSelectedPositionPreview(step, "선택한 직책");
        }

        PermissionDefinition? definitionOrNull = mPermissionDefinitions.FirstOrDefault(definition => definition.Key == step.RequiredPermissionKey);
        if (definitionOrNull == null)
        {
            return "선택한 권한 보유자";
        }

        return definitionOrNull.DisplayName + " 권한 보유자";
    }

    private string getSelectedPositionPreview(ApprovalWorkflowStepFormModel step, string fallback)
    {
        OrganizationPosition? positionOrNull = mPositions.FirstOrDefault(position => step.ApproverPositionID.HasValue && position.ID == step.ApproverPositionID.Value);
        if (positionOrNull == null)
        {
            return fallback;
        }

        return getPositionDisplayName(positionOrNull);
    }

    private string getDefaultStepName(int stepOrder)
    {
        return stepOrder.ToString(CultureInfo.InvariantCulture) + "차 결재";
    }

    private void showResult(ServiceResult result, string successMessage)
    {
        if (result.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = result.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }
}
