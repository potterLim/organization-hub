using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed partial class ApprovalWorkflowManagementService : IApprovalWorkflowManagementService
{
    private const int PRIORITY_STEP = 10;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    public ApprovalWorkflowManagementService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<ApprovalWorkflow>> GetWorkflowsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<ApprovalWorkflow> workflows = await dbContext.ApprovalWorkflows
                .Include(workflow => workflow.AppliesToTeam)
                .Include(workflow => workflow.AppliesToPosition)
                .ThenInclude(position => position!.Team)
                .Include(workflow => workflow.Steps)
                .ThenInclude(step => step.ApproverUser)
                .Include(workflow => workflow.Steps)
                .ThenInclude(step => step.ApproverPosition)
                .Include(workflow => workflow.Steps)
                .ThenInclude(step => step.ApproverTeam)
                .OrderBy(workflow => workflow.Priority)
                .ThenBy(workflow => workflow.Name)
                .ToListAsync(cancellationToken);
            foreach (ApprovalWorkflow workflow in workflows)
            {
                workflow.Steps = workflow.Steps.OrderBy(step => step.StepOrder).ToList();
            }

            return workflows;
        }
    }

    public async Task<IReadOnlyList<UserAccount>> GetApproverUsersAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UserAccount> users = await dbContext.UserAccounts
                .Include(user => user.Team)
                .Include(user => user.Position)
                .Where(user => user.IsActive)
                .OrderBy(user => user.DisplayName)
                .ToListAsync(cancellationToken);
            return users;
        }
    }

    public async Task<ServiceResult> CreateWorkflowAsync(ApprovalWorkflowFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult validationResult = await validateWorkflowFormAsync(formModel, cancellationToken);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalWorkflow workflow = new ApprovalWorkflow();
            applyWorkflowForm(workflow, formModel);
            dbContext.ApprovalWorkflows.Add(workflow);
            addWorkflowSteps(dbContext, workflow, formModel);
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    public async Task<ServiceResult> UpdateWorkflowAsync(ApprovalWorkflowFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult validationResult = await validateWorkflowFormAsync(formModel, cancellationToken);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalWorkflowId workflowId = formModel.WorkflowIdValue;
            int requiredWorkflowId = workflowId.GetRequiredValue();
            ApprovalWorkflow? workflowOrNull = await dbContext.ApprovalWorkflows
                .Include(workflow => workflow.Steps)
                .FirstOrDefaultAsync(workflow => workflow.Id == requiredWorkflowId, cancellationToken);
            if (workflowOrNull == null)
            {
                return ServiceResult.Fail("결재 경로를 찾을 수 없습니다.");
            }

            if (workflowOrNull.Revision != formModel.Revision)
            {
                return AdministrativeSaveUtility.CreateConflictResult();
            }
            applyWorkflowForm(workflowOrNull, formModel);
            dbContext.ApprovalWorkflowSteps.RemoveRange(workflowOrNull.Steps);
            addWorkflowSteps(dbContext, workflowOrNull, formModel);
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    public async Task<ServiceResult> UpdateWorkflowOrderAsync(ApprovalWorkflowOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedWorkflowIds.Count == 0)
        {
            return ServiceResult.Succeed();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (OrderUpdateRequestValidation.hasDuplicateValues(request.OrderedWorkflowIds))
            {
                return ServiceResult.Fail("정렬할 결재 경로가 중복되어 있습니다.");
            }

            List<int> orderedWorkflowIdValues = request.OrderedWorkflowIds.Select(workflowId => workflowId.GetRequiredValue()).ToList();
            List<ApprovalWorkflow> workflows = await dbContext.ApprovalWorkflows.Where(workflow => orderedWorkflowIdValues.Contains(workflow.Id)).ToListAsync(cancellationToken);
            if (workflows.Count != orderedWorkflowIdValues.Count)
            {
                return ServiceResult.Fail("정렬할 결재 경로를 찾을 수 없습니다.");
            }

            Dictionary<int, ApprovalWorkflow> workflowsById = workflows.ToDictionary(workflow => workflow.Id);
            int priority = PRIORITY_STEP;
            foreach (ApprovalWorkflowId workflowId in request.OrderedWorkflowIds)
            {
                ApprovalWorkflow workflow = workflowsById[workflowId.GetRequiredValue()];
                workflow.Priority = priority;
                priority += PRIORITY_STEP;
            }

            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    public async Task<ServiceResult> ActivateWorkflowAsync(ApprovalWorkflowId workflowId, CancellationToken cancellationToken)
    {
        return await setWorkflowActivationStateAsync(workflowId, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivateWorkflowAsync(ApprovalWorkflowId workflowId, CancellationToken cancellationToken)
    {
        return await setWorkflowActivationStateAsync(workflowId, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setWorkflowActivationStateAsync(
        ApprovalWorkflowId workflowId,
        EActivationState activationState,
        CancellationToken cancellationToken
    )
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredWorkflowId = workflowId.GetRequiredValue();
            ApprovalWorkflow? workflowOrNull = await dbContext.ApprovalWorkflows.FirstOrDefaultAsync(workflow => workflow.Id == requiredWorkflowId, cancellationToken);
            if (workflowOrNull == null)
            {
                return ServiceResult.Fail("결재 경로를 찾을 수 없습니다.");
            }

            workflowOrNull.IsActive = activationState == EActivationState.Active;
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

}
