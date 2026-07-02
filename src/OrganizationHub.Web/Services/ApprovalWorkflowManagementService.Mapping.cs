using System.Linq;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed partial class ApprovalWorkflowManagementService
{
    private void applyWorkflowForm(ApprovalWorkflow workflow, ApprovalWorkflowFormModel formModel)
    {
        workflow.Name = formModel.Name.Trim();
        workflow.Description = getTrimmedTextOrEmpty(formModel.Description);
        workflow.AppliesToTeamId = OrganizationTeamId.GetValueOrNull(formModel.AppliesToTeamIdOrNull);
        workflow.AppliesToPositionId = OrganizationPositionId.GetValueOrNull(formModel.AppliesToPositionIdOrNull);
        workflow.Priority = formModel.Priority;
        workflow.IsActive = formModel.IsActive;
    }

    private void addWorkflowSteps(OrganizationHubDbContext dbContext, ApprovalWorkflow workflow, ApprovalWorkflowFormModel formModel)
    {
        foreach (ApprovalWorkflowStepFormModel stepFormModel in formModel.Steps.OrderBy(step => step.StepOrder))
        {
            ApprovalWorkflowStep step = new ApprovalWorkflowStep();
            step.ApprovalWorkflow = workflow;
            step.StepOrder = stepFormModel.StepOrder;
            step.StepName = stepFormModel.StepName.Trim();
            applyWorkflowStepApproverSelection(step, stepFormModel);
            step.ScopeType = stepFormModel.ScopeType;
            step.IsRequired = stepFormModel.IsRequired;
            dbContext.ApprovalWorkflowSteps.Add(step);
        }
    }

    private void applyWorkflowStepApproverSelection(ApprovalWorkflowStep step, ApprovalWorkflowStepFormModel formModel)
    {
        EApproverSelectionType approverSelectionType = formModel.ApproverSelectionType;
        step.ApproverSelectionType = approverSelectionType;
        step.ApproverUserId = null;
        step.ApproverPositionId = null;
        step.ApproverTeamId = null;
        step.RequiredPermissionKey = string.Empty;

        if (approverSelectionType == EApproverSelectionType.SpecificUser)
        {
            step.ApproverUserId = ApprovalUserId.GetValueOrNull(formModel.ApproverUserIdOrNull);
        }

        if (requiresPositionSelection(approverSelectionType))
        {
            step.ApproverPositionId = OrganizationPositionId.GetValueOrNull(formModel.ApproverPositionIdOrNull);
        }

        if (approverSelectionType == EApproverSelectionType.PositionInSelectedTeam)
        {
            step.ApproverTeamId = OrganizationTeamId.GetValueOrNull(formModel.ApproverTeamIdOrNull);
        }

        if (approverSelectionType == EApproverSelectionType.PermissionHolder)
        {
            step.RequiredPermissionKey = formModel.RequiredPermissionKey;
        }
    }

    private static bool requiresPositionSelection(EApproverSelectionType approverSelectionType)
    {
        return approverSelectionType == EApproverSelectionType.SpecificPosition ||
            approverSelectionType == EApproverSelectionType.PositionInRequesterTeam ||
            approverSelectionType == EApproverSelectionType.PositionInSelectedTeam;
    }

    private string getTrimmedTextOrEmpty(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Trim();
    }
}
