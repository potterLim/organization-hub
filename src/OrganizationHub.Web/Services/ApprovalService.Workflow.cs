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

public sealed partial class ApprovalService
{
    private async Task<ConfiguredApprovalLine?> createConfiguredApprovalLineOrNullAsync(
        OrganizationHubDbContext dbContext,
        IReadOnlyList<UserAccount> users,
        UserSession userSession,
        CancellationToken cancellationToken)
    {
        OrganizationTeamId? userTeamIdOrNull = createUserTeamIdOrNull(userSession);
        int? userTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(userTeamIdOrNull);
        OrganizationPositionId? userPositionIdOrNull = createUserPositionIdOrNull(users, userSession);
        int? userPositionIdValueOrNull = OrganizationPositionId.GetValueOrNull(userPositionIdOrNull);
        List<ApprovalWorkflow> candidateWorkflows = await dbContext.ApprovalWorkflows
            .Include(workflow => workflow.Steps)
            .Where(workflow =>
                workflow.IsActive &&
                (workflow.AppliesToTeamId == null || workflow.AppliesToTeamId == userTeamIdValueOrNull) &&
                (workflow.AppliesToPositionId == null || workflow.AppliesToPositionId == userPositionIdValueOrNull))
            .ToListAsync(cancellationToken);
        ApprovalWorkflow? workflowOrNull = candidateWorkflows
            .OrderBy(workflow => getWorkflowMatchOrder(workflow, userTeamIdValueOrNull, userPositionIdValueOrNull))
            .ThenBy(workflow => workflow.Priority)
            .FirstOrDefault();
        if (workflowOrNull == null)
        {
            return null;
        }

        List<ApprovalWorkflowStep> steps = workflowOrNull.Steps.OrderBy(step => step.StepOrder).ToList();
        if (ApprovalStepCount.IsSupportedValue(steps.Count) == false)
        {
            return null;
        }

        List<ApprovalUserOption> selectedApprovers = new List<ApprovalUserOption>();
        HashSet<int> usedUserIds = new HashSet<int>();
        foreach (ApprovalWorkflowStep step in steps)
        {
            EApprovalWorkflowStepRole stepRole = step.StepOrder == steps[steps.Count - 1].StepOrder ? EApprovalWorkflowStepRole.Final : EApprovalWorkflowStepRole.Intermediate;
            UserAccount? approverOrNull = resolveWorkflowStepApproverOrNull(users, userSession, step, usedUserIds, stepRole);
            if (approverOrNull == null)
            {
                if (step.IsRequired)
                {
                    return null;
                }

                continue;
            }

            usedUserIds.Add(approverOrNull.Id);
            selectedApprovers.Add(createApprovalUserOption(approverOrNull));
        }

        if (selectedApprovers.Count <= 0)
        {
            return null;
        }

        ApprovalUserOption finalApprover = selectedApprovers[selectedApprovers.Count - 1];
        List<ApprovalUserOption> intermediateApprovers = selectedApprovers.Take(selectedApprovers.Count - 1).ToList();
        ApprovalStepCount selectedStepCount = new ApprovalStepCount(selectedApprovers.Count);
        ApprovalDefaultLineOption defaultLine = new ApprovalDefaultLineOption();
        defaultLine.StepCount = selectedStepCount;
        defaultLine.FinalApproverUserIdValue = ApprovalUserId.CreateExisting(finalApprover.UserId);
        if (selectedStepCount.RequiresFirstApprover())
        {
            defaultLine.FirstApproverUserIdValue = ApprovalUserId.CreateExisting(selectedApprovers[0].UserId);
        }

        if (selectedStepCount.RequiresSecondApprover())
        {
            defaultLine.SecondApproverUserIdValue = ApprovalUserId.CreateExisting(selectedApprovers[1].UserId);
        }

        return new ConfiguredApprovalLine(selectedStepCount, intermediateApprovers, new List<ApprovalUserOption> { finalApprover }, defaultLine);
    }

    private OrganizationPositionId? createUserPositionIdOrNull(
        IReadOnlyList<UserAccount> users,
        UserSession userSession)
    {
        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        UserAccount? userAccountOrNull = users.FirstOrDefault(user => user.Id == currentUserId);
        if (userAccountOrNull == null || userAccountOrNull.PositionId.HasValue == false)
        {
            return null;
        }

        return OrganizationPositionId.CreateExisting(userAccountOrNull.PositionId.GetValueOrDefault());
    }

    private EApprovalWorkflowMatchOrder getWorkflowMatchOrder(
        ApprovalWorkflow workflow,
        int? userTeamIdValueOrNull,
        int? userPositionIdValueOrNull)
    {
        bool isExactTeamMatch = workflow.AppliesToTeamId.HasValue && workflow.AppliesToTeamId == userTeamIdValueOrNull;
        bool isExactPositionMatch = workflow.AppliesToPositionId.HasValue && workflow.AppliesToPositionId == userPositionIdValueOrNull;
        if (isExactTeamMatch && isExactPositionMatch)
        {
            return EApprovalWorkflowMatchOrder.TeamAndPosition;
        }

        if (isExactTeamMatch)
        {
            return EApprovalWorkflowMatchOrder.Team;
        }

        if (isExactPositionMatch)
        {
            return EApprovalWorkflowMatchOrder.Position;
        }

        return EApprovalWorkflowMatchOrder.Global;
    }

    private UserAccount? resolveWorkflowStepApproverOrNull(
        IReadOnlyList<UserAccount> users,
        UserSession userSession,
        ApprovalWorkflowStep step,
        IReadOnlySet<int> usedUserIds,
        EApprovalWorkflowStepRole stepRole)
    {
        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        List<UserAccount> candidates = getWorkflowStepCandidates(users, userSession, step, stepRole)
            .Where(user => user.Id != currentUserId && usedUserIds.Contains(user.Id) == false)
            .Where(user => hasUserWorkflowPermission(user, userSession, step, stepRole))
            .OrderBy(user => getApproverSortOrder(user, userSession))
            .ThenBy(user => user.DisplayName)
            .ToList();
        return candidates.FirstOrDefault();
    }

    private List<UserAccount> getWorkflowStepCandidates(
        IReadOnlyList<UserAccount> users,
        UserSession userSession,
        ApprovalWorkflowStep step,
        EApprovalWorkflowStepRole stepRole)
    {
        switch (step.ApproverSelectionType)
        {
            case EApproverSelectionType.SpecificUser:
                return getSpecificUserCandidates(users, step);
            case EApproverSelectionType.SpecificPosition:
                return getSpecificPositionCandidates(users, step);
            case EApproverSelectionType.PositionInRequesterTeam:
                return getRequesterTeamPositionCandidates(users, userSession, step);
            case EApproverSelectionType.PositionInSelectedTeam:
                return getSelectedTeamPositionCandidates(users, step);
            case EApproverSelectionType.PermissionHolder:
                return users.Where(user => hasUserWorkflowPermission(user, userSession, step, stepRole)).ToList();
            default:
                return new List<UserAccount>();
        }
    }

    private List<UserAccount> getSpecificUserCandidates(
        IReadOnlyList<UserAccount> users,
        ApprovalWorkflowStep step)
    {
        if (step.ApproverUserId.HasValue == false)
        {
            return new List<UserAccount>();
        }

        int approverUserId = step.ApproverUserId.GetValueOrDefault();
        return users
            .Where(user => user.Id == approverUserId)
            .ToList();
    }

    private List<UserAccount> getSpecificPositionCandidates(
        IReadOnlyList<UserAccount> users,
        ApprovalWorkflowStep step)
    {
        if (step.ApproverPositionId.HasValue == false)
        {
            return new List<UserAccount>();
        }

        int approverPositionId = step.ApproverPositionId.GetValueOrDefault();
        return users
            .Where(user => user.PositionId == approverPositionId)
            .ToList();
    }

    private List<UserAccount> getRequesterTeamPositionCandidates(
        IReadOnlyList<UserAccount> users,
        UserSession userSession,
        ApprovalWorkflowStep step)
    {
        if (userSession.HasTeam == false || step.ApproverPositionId.HasValue == false)
        {
            return new List<UserAccount>();
        }

        int requesterTeamId = userSession.GetRequiredOrganizationTeamId().GetRequiredValue();
        int approverPositionId = step.ApproverPositionId.GetValueOrDefault();
        return users
            .Where(user =>
                user.TeamId == requesterTeamId &&
                user.PositionId == approverPositionId)
            .ToList();
    }

    private List<UserAccount> getSelectedTeamPositionCandidates(
        IReadOnlyList<UserAccount> users,
        ApprovalWorkflowStep step)
    {
        if (step.ApproverTeamId.HasValue == false || step.ApproverPositionId.HasValue == false)
        {
            return new List<UserAccount>();
        }

        int approverTeamId = step.ApproverTeamId.GetValueOrDefault();
        int approverPositionId = step.ApproverPositionId.GetValueOrDefault();
        return users
            .Where(user =>
                user.TeamId == approverTeamId &&
                user.PositionId == approverPositionId)
            .ToList();
    }

    private bool hasUserWorkflowPermission(UserAccount user, UserSession userSession, ApprovalWorkflowStep step, EApprovalWorkflowStepRole stepRole)
    {
        PermissionKey permissionKey = getWorkflowStepPermissionKey(step, stepRole);
        return hasUserApprovalPermission(user, userSession, permissionKey);
    }

    private PermissionKey getWorkflowStepPermissionKey(ApprovalWorkflowStep step, EApprovalWorkflowStepRole stepRole)
    {
        if (string.IsNullOrWhiteSpace(step.RequiredPermissionKey) == false)
        {
            return new PermissionKey(step.RequiredPermissionKey);
        }

        if (stepRole == EApprovalWorkflowStepRole.Final)
        {
            return new PermissionKey(PermissionKeys.APPROVAL_FINAL_APPROVE);
        }

        return new PermissionKey(PermissionKeys.APPROVAL_APPROVE);
    }

    private enum EApprovalWorkflowStepRole
    {
        Intermediate,
        Final
    }

    private enum EApprovalWorkflowMatchOrder
    {
        TeamAndPosition = 0,
        Team = 1,
        Position = 2,
        Global = 3
    }
}
