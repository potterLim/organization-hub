using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private IReadOnlyList<int> createAvailableStepCounts(
        UserSession userSession,
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions
    )
    {
        List<int> stepCounts = new List<int>();
        if (finalApproverOptions.Count == 0)
        {
            return stepCounts;
        }

        if (UserPermissionPolicy.CanCreateApprovalRequests(userSession.Role) == false)
        {
            return stepCounts;
        }

        if (userSession.Role == EUserRole.TeamLeader || userSession.IsManagementRole())
        {
            addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, new ApprovalStepCount(1));
            addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, new ApprovalStepCount(2));
            addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, new ApprovalStepCount(3));
            return stepCounts;
        }

        addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, new ApprovalStepCount(2));
        addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, new ApprovalStepCount(3));
        return stepCounts;
    }

    private void addStepCountIfAvailable(
        List<int> stepCounts,
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        ApprovalStepCount stepCount
    )
    {
        if (canBuildApprovalLine(approverOptions, finalApproverOptions, stepCount))
        {
            stepCounts.Add(stepCount.Value);
        }
    }

    private bool canBuildApprovalLine(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        ApprovalStepCount stepCount
    )
    {
        int requiredIntermediateApproverCount = stepCount.Value - 1;
        foreach (ApprovalUserOption finalApproverOption in finalApproverOptions)
        {
            int availableIntermediateApproverCount = approverOptions.Where(option => option.UserId != finalApproverOption.UserId).Select(option => option.UserId).Distinct().Count();
            if (availableIntermediateApproverCount >= requiredIntermediateApproverCount)
            {
                return true;
            }
        }

        return false;
    }
}
