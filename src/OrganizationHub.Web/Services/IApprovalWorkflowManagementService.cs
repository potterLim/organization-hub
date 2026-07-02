using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal interface IApprovalWorkflowManagementService
{
    Task<IReadOnlyList<ApprovalWorkflow>> GetWorkflowsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> GetApproverUsersAsync(CancellationToken cancellationToken);
    Task<ServiceResult> CreateWorkflowAsync(ApprovalWorkflowFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateWorkflowAsync(ApprovalWorkflowFormModel formModel, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateWorkflowOrderAsync(ApprovalWorkflowOrderUpdateRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> ActivateWorkflowAsync(ApprovalWorkflowId workflowId, CancellationToken cancellationToken);
    Task<ServiceResult> DeactivateWorkflowAsync(ApprovalWorkflowId workflowId, CancellationToken cancellationToken);
}
