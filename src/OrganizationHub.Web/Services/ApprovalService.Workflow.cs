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
        ApprovalWorkflow? workflowOrNull = await dbContext.ApprovalWorkflows
            .Include(workflow => workflow.Steps)
            .Where(workflow => workflow.IsActive && (workflow.AppliesToTeamId == null || workflow.AppliesToTeamId == userTeamIdValueOrNull))
            .OrderBy(workflow => workflow.AppliesToTeamId == userTeamIdValueOrNull ? 0 : 1)
            .ThenBy(workflow => workflow.Priority)
            .FirstOrDefaultAsync(cancellationToken);
        if (workflowOrNull == null)
        {
            return null;
        }

        List<ApprovalWorkflowStep> steps = workflowOrNull.Steps.OrderBy(step => step.StepOrder).ToList();
        if (steps.Count <= 0 || steps.Count > 3)
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
        defaultLine.FinalApproverUserId = finalApprover.UserId;
        if (selectedApprovers.Count >= 2)
        {
            defaultLine.FirstApproverUserId = selectedApprovers[0].UserId;
        }

        if (selectedApprovers.Count == 3)
        {
            defaultLine.SecondApproverUserId = selectedApprovers[1].UserId;
        }

        return new ConfiguredApprovalLine(selectedStepCount, intermediateApprovers, new List<ApprovalUserOption> { finalApprover }, defaultLine);
    }

    private UserAccount? resolveWorkflowStepApproverOrNull(
        IReadOnlyList<UserAccount> users,
        UserSession userSession,
        ApprovalWorkflowStep step,
        IReadOnlySet<int> usedUserIds,
        EApprovalWorkflowStepRole stepRole)
    {
        List<UserAccount> candidates = getWorkflowStepCandidates(users, userSession, step, stepRole)
            .Where(user => user.Id != userSession.UserId && usedUserIds.Contains(user.Id) == false)
            .Where(user => userHasWorkflowPermission(user, userSession, step, stepRole))
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
                return users.Where(user => userHasWorkflowPermission(user, userSession, step, stepRole)).ToList();
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

        int requesterTeamId = userSession.GetRequiredTeamIdValue();
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

    private bool userHasWorkflowPermission(UserAccount user, UserSession userSession, ApprovalWorkflowStep step, EApprovalWorkflowStepRole stepRole)
    {
        string permissionKey = getWorkflowStepPermissionKey(step, stepRole);
        return userHasApprovalPermission(user, userSession, permissionKey);
    }

    private string getWorkflowStepPermissionKey(ApprovalWorkflowStep step, EApprovalWorkflowStepRole stepRole)
    {
        if (string.IsNullOrWhiteSpace(step.RequiredPermissionKey) == false)
        {
            return step.RequiredPermissionKey;
        }

        if (stepRole == EApprovalWorkflowStepRole.Final)
        {
            return PermissionKeys.APPROVAL_FINAL_APPROVE;
        }

        return PermissionKeys.APPROVAL_APPROVE;
    }

    private enum EApprovalWorkflowStepRole
    {
        Intermediate,
        Final
    }
}
