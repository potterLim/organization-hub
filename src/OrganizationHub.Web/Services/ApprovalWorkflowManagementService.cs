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

public sealed class ApprovalWorkflowManagementService : IApprovalWorkflowManagementService
{
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
            List<UserAccount> users = await dbContext.UserAccounts.Include(user => user.Team).Include(user => user.Position).Where(user => user.IsActive).OrderBy(user => user.DisplayName).ToListAsync(cancellationToken);
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
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
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
            ApprovalWorkflow? workflowOrNull = await dbContext.ApprovalWorkflows.Include(workflow => workflow.Steps).FirstOrDefaultAsync(workflow => workflow.ID == formModel.ID, cancellationToken);
            if (workflowOrNull == null)
            {
                return ServiceResult.Fail("결재 경로를 찾을 수 없습니다.");
            }

            applyWorkflowForm(workflowOrNull, formModel);
            dbContext.ApprovalWorkflowSteps.RemoveRange(workflowOrNull.Steps);
            addWorkflowSteps(dbContext, workflowOrNull, formModel);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> ActivateWorkflowAsync(int workflowID, CancellationToken cancellationToken)
    {
        return await setWorkflowActivationStateAsync(workflowID, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivateWorkflowAsync(int workflowID, CancellationToken cancellationToken)
    {
        return await setWorkflowActivationStateAsync(workflowID, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setWorkflowActivationStateAsync(int workflowID, EActivationState activationState, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalWorkflow? workflowOrNull = await dbContext.ApprovalWorkflows.FirstOrDefaultAsync(workflow => workflow.ID == workflowID, cancellationToken);
            if (workflowOrNull == null)
            {
                return ServiceResult.Fail("결재 경로를 찾을 수 없습니다.");
            }

            workflowOrNull.IsActive = activationState == EActivationState.Active;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private async Task<ServiceResult> validateWorkflowFormAsync(ApprovalWorkflowFormModel formModel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            return ServiceResult.Fail("결재 경로 이름을 입력해 주세요.");
        }

        if (formModel.Steps.Count == 0)
        {
            return ServiceResult.Fail("결재 단계가 하나 이상 필요합니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IReadOnlySet<int> teamIDs = await dbContext.Teams.Select(team => team.ID).ToHashSetAsync(cancellationToken);
            IReadOnlySet<int> userIDs = await dbContext.UserAccounts.Select(user => user.ID).ToHashSetAsync(cancellationToken);
            IReadOnlySet<int> positionIDs = await dbContext.Positions.Select(position => position.ID).ToHashSetAsync(cancellationToken);
            IReadOnlySet<string> permissionKeys = PermissionCatalog.GetDefinitions().Select(definition => definition.Key).ToHashSet();

            if (formModel.AppliesToTeamID.HasValue && teamIDs.Contains(formModel.AppliesToTeamID.Value) == false)
            {
                return ServiceResult.Fail("적용 팀을 찾을 수 없습니다.");
            }

            foreach (ApprovalWorkflowStepFormModel stepFormModel in formModel.Steps.OrderBy(step => step.StepOrder))
            {
                ServiceResult stepValidationResult = validateWorkflowStep(stepFormModel, teamIDs, userIDs, positionIDs, permissionKeys);
                if (stepValidationResult.IsSuccess == false)
                {
                    return stepValidationResult;
                }
            }
        }

        return ServiceResult.Succeed();
    }

    private ServiceResult validateWorkflowStep(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<int> teamIDs, IReadOnlySet<int> userIDs, IReadOnlySet<int> positionIDs, IReadOnlySet<string> permissionKeys)
    {
        if (string.IsNullOrWhiteSpace(stepFormModel.StepName))
        {
            return ServiceResult.Fail("결재 단계 이름을 입력해 주세요.");
        }

        switch (stepFormModel.ApproverSelectionType)
        {
            case EApproverSelectionType.SpecificUser:
                return validateRequiredUser(stepFormModel, userIDs);
            case EApproverSelectionType.SpecificPosition:
            case EApproverSelectionType.PositionInRequesterTeam:
                return validateRequiredPosition(stepFormModel, positionIDs);
            case EApproverSelectionType.PositionInSelectedTeam:
                return validateRequiredPositionAndTeam(stepFormModel, positionIDs, teamIDs);
            case EApproverSelectionType.PermissionHolder:
                return validateRequiredPermission(stepFormModel, permissionKeys);
            default:
                return ServiceResult.Fail("알 수 없는 승인자 선택 방식입니다.");
        }
    }

    private ServiceResult validateRequiredUser(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<int> userIDs)
    {
        if (stepFormModel.ApproverUserID.HasValue && userIDs.Contains(stepFormModel.ApproverUserID.Value))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("특정 사용자 승인 단계에는 승인자를 선택해 주세요.");
    }

    private ServiceResult validateRequiredPosition(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<int> positionIDs)
    {
        if (stepFormModel.ApproverPositionID.HasValue && positionIDs.Contains(stepFormModel.ApproverPositionID.Value))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("직책 기준 승인 단계에는 직책을 선택해 주세요.");
    }

    private ServiceResult validateRequiredPositionAndTeam(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<int> positionIDs, IReadOnlySet<int> teamIDs)
    {
        ServiceResult positionValidationResult = validateRequiredPosition(stepFormModel, positionIDs);
        if (positionValidationResult.IsSuccess == false)
        {
            return positionValidationResult;
        }

        if (stepFormModel.ApproverTeamID.HasValue && teamIDs.Contains(stepFormModel.ApproverTeamID.Value))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("특정 팀 직책 승인 단계에는 팀을 선택해 주세요.");
    }

    private ServiceResult validateRequiredPermission(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<string> permissionKeys)
    {
        if (string.IsNullOrWhiteSpace(stepFormModel.RequiredPermissionKey) == false && permissionKeys.Contains(stepFormModel.RequiredPermissionKey))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("권한 보유자 승인 단계에는 권한을 선택해 주세요.");
    }

    private void applyWorkflowForm(ApprovalWorkflow workflow, ApprovalWorkflowFormModel formModel)
    {
        workflow.Name = formModel.Name.Trim();
        workflow.Description = getEmptyAsEmpty(formModel.Description);
        workflow.AppliesToTeamID = formModel.AppliesToTeamID;
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
            step.ApproverSelectionType = stepFormModel.ApproverSelectionType;
            step.ApproverUserID = stepFormModel.ApproverSelectionType == EApproverSelectionType.SpecificUser ? stepFormModel.ApproverUserID : null;
            step.ApproverPositionID = usesPosition(stepFormModel.ApproverSelectionType) ? stepFormModel.ApproverPositionID : null;
            step.ApproverTeamID = stepFormModel.ApproverSelectionType == EApproverSelectionType.PositionInSelectedTeam ? stepFormModel.ApproverTeamID : null;
            step.RequiredPermissionKey = stepFormModel.ApproverSelectionType == EApproverSelectionType.PermissionHolder ? stepFormModel.RequiredPermissionKey : string.Empty;
            step.ScopeType = stepFormModel.ScopeType;
            step.IsRequired = stepFormModel.IsRequired;
            dbContext.ApprovalWorkflowSteps.Add(step);
        }
    }

    private bool usesPosition(EApproverSelectionType approverSelectionType)
    {
        return approverSelectionType == EApproverSelectionType.SpecificPosition ||
            approverSelectionType == EApproverSelectionType.PositionInRequesterTeam ||
            approverSelectionType == EApproverSelectionType.PositionInSelectedTeam;
    }

    private string getEmptyAsEmpty(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }
}
