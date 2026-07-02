using System.Collections.Generic;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
    private string getWorkflowConditionName(ApprovalWorkflow workflow)
    {
        if (workflow.AppliesToTeam == null && workflow.AppliesToPosition == null)
        {
            return "전체 요청자";
        }

        if (workflow.AppliesToTeam == null)
        {
            return getWorkflowPositionName(workflow);
        }

        if (workflow.AppliesToPosition == null)
        {
            return workflow.AppliesToTeam.DisplayName + " 전체 직책";
        }

        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(
            workflow.AppliesToTeam.DisplayName,
            workflow.AppliesToPosition.Name);
        return affiliationLabel.GetFullText();
    }

    private string getWorkflowPositionName(ApprovalWorkflow workflow)
    {
        if (workflow.AppliesToPosition == null)
        {
            return "전체 직책";
        }

        return getPositionDisplayName(workflow.AppliesToPosition);
    }

    private List<OrganizationPosition> getRequesterPositionOptions()
    {
        List<OrganizationPosition> positions = mPositions;
        if (mFormModel.AppliesToTeamId.HasValue)
        {
            positions = mPositions.Where(position => position.TeamId == mFormModel.AppliesToTeamId.Value).ToList();
        }

        return positions.OrderBy(position => position.Team?.SortOrder).ThenBy(position => position.SortOrder).ThenBy(position => position.Name).ToList();
    }

    private string getPositionDisplayName(OrganizationPosition position)
    {
        if (position.Team == null)
        {
            return position.Name;
        }

        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(position.Team.DisplayName, position.Name);
        return affiliationLabel.GetFullText();
    }

    private string getUserDisplayName(UserAccount user)
    {
        string positionName = user.DisplayName;
        if (user.Position != null)
        {
            positionName = user.Position.Name;
        }

        string teamName = string.Empty;
        if (user.Team != null)
        {
            teamName = user.Team.DisplayName;
        }

        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(teamName, positionName);
        return affiliationLabel.GetFullText();
    }

    private string getWorkflowTargetPreview()
    {
        Team? teamOrNull = null;
        if (mFormModel.AppliesToTeamId.HasValue)
        {
            teamOrNull = mTeams.FirstOrDefault(team => team.Id == mFormModel.AppliesToTeamId.Value);
        }

        OrganizationPosition? positionOrNull = null;
        if (mFormModel.AppliesToPositionId.HasValue)
        {
            positionOrNull = mPositions.FirstOrDefault(position => position.Id == mFormModel.AppliesToPositionId.Value);
        }

        string targetText = getWorkflowTargetText(teamOrNull, positionOrNull);
        return targetText + " 요청에 적용됩니다.";
    }

    private string getWorkflowTargetText(
        Team? teamOrNull,
        OrganizationPosition? positionOrNull)
    {
        if (teamOrNull == null && positionOrNull == null)
        {
            return "전체 요청자";
        }

        if (teamOrNull == null)
        {
            if (positionOrNull == null)
            {
                return "선택한 직책";
            }

            return getPositionDisplayName(positionOrNull);
        }

        if (positionOrNull == null)
        {
            return teamOrNull.DisplayName + " 전체 직책";
        }

        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(teamOrNull.DisplayName, positionOrNull.Name);
        return affiliationLabel.GetFullText();
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
            return "요청자 팀의 " + getSelectedPositionName(step, "선택한 직책");
        }

        if (step.ApproverSelectionType == EApproverSelectionType.PositionInSelectedTeam)
        {
            string teamText = "선택한 팀";
            Team? teamOrNull = mTeams.FirstOrDefault(team => step.ApproverTeamId.HasValue && team.Id == step.ApproverTeamId.Value);
            if (teamOrNull != null)
            {
                teamText = teamOrNull.DisplayName;
            }

            return teamText + "의 " + getSelectedPositionName(step, "선택한 직책");
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

    private string getSelectedPositionName(ApprovalWorkflowStepFormModel step, string fallback)
    {
        OrganizationPosition? positionOrNull = mPositions.FirstOrDefault(position => step.ApproverPositionId.HasValue && position.Id == step.ApproverPositionId.Value);
        if (positionOrNull == null)
        {
            return fallback;
        }

        return positionOrNull.Name;
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
