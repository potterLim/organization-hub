using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
    private async Task saveWorkflowAsync()
    {
        ServiceResult saveResult;
        if (mFormModel.Id == 0)
        {
            saveResult = await ApprovalWorkflowManagementService.CreateWorkflowAsync(mFormModel, CancellationToken.None);
        }
        else
        {
            saveResult = await ApprovalWorkflowManagementService.UpdateWorkflowAsync(mFormModel, CancellationToken.None);
        }

        mHasSaveConflict = saveResult.IsConcurrencyConflict;
        showOperationResult(saveResult, "결재 경로 설정이 저장되었습니다.");
        if (saveResult.IsSuccess)
        {
            await loadDataAsync();
            clearForm();
        }
    }

    private async Task toggleWorkflowActiveAsync(ApprovalWorkflow workflow)
    {
        ApprovalWorkflowId workflowId = ApprovalWorkflowId.CreateExisting(workflow.Id);
        ServiceResult activationResult;
        if (workflow.IsActive)
        {
            activationResult = await ApprovalWorkflowManagementService.DeactivateWorkflowAsync(workflowId, CancellationToken.None);
        }
        else
        {
            activationResult = await ApprovalWorkflowManagementService.ActivateWorkflowAsync(workflowId, CancellationToken.None);
        }

        showOperationResult(activationResult, "결재 경로 상태가 변경되었습니다.");
        await loadDataAsync();
    }
}
