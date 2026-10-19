using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
    private void editWorkflow(ApprovalWorkflow workflow)
    {
        mFormModel = new ApprovalWorkflowFormModel();
        mFormModel.Id = workflow.Id;
        mFormModel.Name = workflow.Name;
        mFormModel.Description = workflow.Description;
        mFormModel.AppliesToTeamId = workflow.AppliesToTeamId;
        mFormModel.AppliesToPositionId = workflow.AppliesToPositionId;
        mFormModel.Priority = workflow.Priority;
        mFormModel.IsActive = workflow.IsActive;
        foreach (ApprovalWorkflowStep step in workflow.Steps.OrderBy(step => step.StepOrder))
        {
            ApprovalWorkflowStepFormModel stepFormModel = new ApprovalWorkflowStepFormModel();
            stepFormModel.Id = step.Id;
            stepFormModel.StepOrder = step.StepOrder;
            stepFormModel.StepName = step.StepName;
            stepFormModel.ApproverSelectionType = step.ApproverSelectionType;
            stepFormModel.ApproverUserId = step.ApproverUserId;
            stepFormModel.ApproverPositionId = step.ApproverPositionId;
            stepFormModel.ApproverTeamId = step.ApproverTeamId;
            stepFormModel.RequiredPermissionKey = step.RequiredPermissionKey;
            stepFormModel.ScopeType = PermissionScopeSupport.GetEditableScope(step.ScopeType);
            stepFormModel.IsRequired = step.IsRequired;
            mFormModel.Steps.Add(stepFormModel);
        }

        mFormTitle = "결재 경로 수정";
    }

    private void clearForm()
    {
        mFormModel = new ApprovalWorkflowFormModel();
        mFormModel.Priority = getNextWorkflowPriority();
        mFormModel.IsActive = true;
        addStep();
        mFormTitle = "새 결재 경로";
    }

    private void addStep()
    {
        int stepOrder = mFormModel.Steps.Count + 1;
        ApprovalWorkflowStepFormModel step = new ApprovalWorkflowStepFormModel();
        step.StepOrder = stepOrder;
        step.IsRequired = true;
        mFormModel.Steps.Add(step);
        renumberSteps();
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
        int stepOrder = ApprovalStepNumber.MINIMUM_VALUE;
        ApprovalStepCount stepCount = new ApprovalStepCount(mFormModel.Steps.Count);
        foreach (ApprovalWorkflowStepFormModel step in mFormModel.Steps.OrderBy(step => step.StepOrder))
        {
            step.StepOrder = stepOrder;
            step.StepName = ApprovalStepLabelUtility.GetStepLabel(stepCount, new ApprovalStepNumber(stepOrder));
            stepOrder++;
        }
    }

    private void setAppliesToTeamId(int? teamIdOrNull)
    {
        mFormModel.AppliesToTeamId = teamIdOrNull;
        if (teamIdOrNull.HasValue == false)
        {
            return;
        }

        OrganizationPosition? selectedPositionOrNull = mPositions.FirstOrDefault(position =>
            mFormModel.AppliesToPositionId.HasValue &&
            position.Id == mFormModel.AppliesToPositionId.Value);
        if (selectedPositionOrNull == null || selectedPositionOrNull.TeamId != teamIdOrNull.Value)
        {
            mFormModel.AppliesToPositionId = null;
        }
    }

    private void setAppliesToPositionId(int? positionIdOrNull)
    {
        mFormModel.AppliesToPositionId = positionIdOrNull;
    }

    private void setApproverSelectionType(ApprovalWorkflowStepFormModel step, EApproverSelectionType selectionType)
    {
        step.ApproverSelectionType = selectionType;
        step.ApproverUserId = null;
        step.ApproverTeamId = null;
        step.RequiredPermissionKey = string.Empty;
        if (requiresPositionSelection(selectionType) == false)
        {
            step.ApproverPositionId = null;
        }
    }

    private bool requiresPositionSelection(EApproverSelectionType selectionType)
    {
        return selectionType == EApproverSelectionType.SpecificPosition ||
            selectionType == EApproverSelectionType.PositionInRequesterTeam ||
            selectionType == EApproverSelectionType.PositionInSelectedTeam;
    }

}
