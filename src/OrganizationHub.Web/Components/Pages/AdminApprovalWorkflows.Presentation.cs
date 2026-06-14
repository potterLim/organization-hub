using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
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
        if (mFormModel.AppliesToTeamId.HasValue == false)
        {
            return "모든 팀 요청에 적용됩니다. 적용 우선순위가 낮은 경로를 먼저 사용합니다.";
        }

        Team? teamOrNull = mTeams.FirstOrDefault(team => team.Id == mFormModel.AppliesToTeamId.Value);
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
            UserAccount? userOrNull = mUsers.FirstOrDefault(user => step.ApproverUserId.HasValue && user.Id == step.ApproverUserId.Value);
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
            Team? teamOrNull = mTeams.FirstOrDefault(team => step.ApproverTeamId.HasValue && team.Id == step.ApproverTeamId.Value);
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
        OrganizationPosition? positionOrNull = mPositions.FirstOrDefault(position => step.ApproverPositionId.HasValue && position.Id == step.ApproverPositionId.Value);
        if (positionOrNull == null)
        {
            return fallback;
        }

        return getPositionDisplayName(positionOrNull);
    }

    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
        if (operationResult.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = operationResult.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }
}
