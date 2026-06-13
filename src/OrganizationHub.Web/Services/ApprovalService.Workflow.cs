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
    private async Task<ConfiguredApprovalLine?> createConfiguredApprovalLineOrNullAsync(OrganizationHubDbContext dbContext, IReadOnlyList<UserAccount> users, UserSession userSession, CancellationToken cancellationToken)
    {
        ApprovalWorkflow? workflowOrNull = await dbContext.ApprovalWorkflows
            .Include(workflow => workflow.Steps)
            .Where(workflow => workflow.IsActive && (workflow.AppliesToTeamID == null || workflow.AppliesToTeamID == userSession.TeamID))
            .OrderBy(workflow => workflow.AppliesToTeamID == userSession.TeamID ? 0 : 1)
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
        HashSet<int> usedUserIDs = new HashSet<int>();
        foreach (ApprovalWorkflowStep step in steps)
        {
            bool isFinalStep = step.StepOrder == steps[steps.Count - 1].StepOrder;
            UserAccount? approverOrNull = resolveWorkflowStepApproverOrNull(users, userSession, step, usedUserIDs, isFinalStep);
            if (approverOrNull == null)
            {
                if (step.IsRequired)
                {
                    return null;
                }

                continue;
            }

            usedUserIDs.Add(approverOrNull.ID);
            selectedApprovers.Add(createApprovalUserOption(approverOrNull));
        }

        if (selectedApprovers.Count <= 0)
        {
            return null;
        }

        ApprovalUserOption finalApprover = selectedApprovers[selectedApprovers.Count - 1];
        List<ApprovalUserOption> intermediateApprovers = selectedApprovers.Take(selectedApprovers.Count - 1).ToList();
        ApprovalDefaultLineOption defaultLine = new ApprovalDefaultLineOption();
        defaultLine.StepCount = selectedApprovers.Count;
        defaultLine.FinalApproverUserID = finalApprover.UserID;
        if (selectedApprovers.Count >= 2)
        {
            defaultLine.FirstApproverUserID = selectedApprovers[0].UserID;
        }

        if (selectedApprovers.Count == 3)
        {
            defaultLine.SecondApproverUserID = selectedApprovers[1].UserID;
        }

        return new ConfiguredApprovalLine(selectedApprovers.Count, intermediateApprovers, new List<ApprovalUserOption> { finalApprover }, defaultLine);
    }

    private UserAccount? resolveWorkflowStepApproverOrNull(IReadOnlyList<UserAccount> users, UserSession userSession, ApprovalWorkflowStep step, IReadOnlySet<int> usedUserIDs, bool isFinalStep)
    {
        List<UserAccount> candidates = getWorkflowStepCandidates(users, userSession, step, isFinalStep)
            .Where(user => user.ID != userSession.UserID && usedUserIDs.Contains(user.ID) == false)
            .OrderBy(user => getApproverSortOrder(user, userSession))
            .ThenBy(user => user.DisplayName)
            .ToList();
        return candidates.FirstOrDefault();
    }

    private List<UserAccount> getWorkflowStepCandidates(IReadOnlyList<UserAccount> users, UserSession userSession, ApprovalWorkflowStep step, bool isFinalStep)
    {
        switch (step.ApproverSelectionType)
        {
            case EApproverSelectionType.SpecificUser:
                return users.Where(user => step.ApproverUserID.HasValue && user.ID == step.ApproverUserID.Value).ToList();
            case EApproverSelectionType.SpecificPosition:
                return users.Where(user => step.ApproverPositionID.HasValue && user.PositionID == step.ApproverPositionID.Value).ToList();
            case EApproverSelectionType.PositionInRequesterTeam:
                return users.Where(user => userSession.TeamID.HasValue && step.ApproverPositionID.HasValue && user.TeamID == userSession.TeamID.Value && user.PositionID == step.ApproverPositionID.Value).ToList();
            case EApproverSelectionType.PositionInSelectedTeam:
                return users.Where(user => step.ApproverTeamID.HasValue && step.ApproverPositionID.HasValue && user.TeamID == step.ApproverTeamID.Value && user.PositionID == step.ApproverPositionID.Value).ToList();
            case EApproverSelectionType.PermissionHolder:
                return users.Where(user => userHasWorkflowPermission(user, userSession, step, isFinalStep)).ToList();
            default:
                return new List<UserAccount>();
        }
    }

    private bool userHasWorkflowPermission(UserAccount user, UserSession userSession, ApprovalWorkflowStep step, bool isFinalStep)
    {
        if (user.Position == null || user.Position.IsActive == false)
        {
            return false;
        }

        string permissionKey = getWorkflowStepPermissionKey(step, isFinalStep);
        foreach (PositionPermissionGrant permissionGrant in user.Position.PermissionGrants)
        {
            if (permissionGrant.IsAllowed == false || permissionGrant.PermissionKey != permissionKey)
            {
                continue;
            }

            if (isWorkflowPermissionGrantInScope(permissionGrant, user, userSession.TeamID))
            {
                return true;
            }
        }

        return false;
    }

    private string getWorkflowStepPermissionKey(ApprovalWorkflowStep step, bool isFinalStep)
    {
        if (string.IsNullOrWhiteSpace(step.RequiredPermissionKey) == false)
        {
            return step.RequiredPermissionKey;
        }

        if (isFinalStep)
        {
            return PermissionKeys.APPROVAL_FINAL_APPROVE;
        }

        return PermissionKeys.APPROVAL_APPROVE;
    }

    private bool isWorkflowPermissionGrantInScope(PositionPermissionGrant permissionGrant, UserAccount user, int? requesterTeamIDOrNull)
    {
        switch (permissionGrant.ScopeType)
        {
            case EPermissionScopeType.Global:
                return true;
            case EPermissionScopeType.OwnTeam:
            case EPermissionScopeType.OwnTeamAndChildren:
                return requesterTeamIDOrNull.HasValue && user.TeamID.HasValue && requesterTeamIDOrNull.Value == user.TeamID.Value;
            case EPermissionScopeType.SelectedTeam:
                return requesterTeamIDOrNull.HasValue && permissionGrant.TargetTeamID.HasValue && requesterTeamIDOrNull.Value == permissionGrant.TargetTeamID.Value;
            default:
                return false;
        }
    }
}
