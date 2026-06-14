using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private IReadOnlyList<int> createAvailableStepCounts(UserSession userSession, IReadOnlyList<ApprovalUserOption> approverOptions, IReadOnlyList<ApprovalUserOption> finalApproverOptions)
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
            addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, 1);
            addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, 2);
            addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, 3);
            return stepCounts;
        }

        addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, 2);
        addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, 3);
        return stepCounts;
    }

    private void addStepCountIfAvailable(List<int> stepCounts, IReadOnlyList<ApprovalUserOption> approverOptions, IReadOnlyList<ApprovalUserOption> finalApproverOptions, int stepCount)
    {
        if (canBuildApprovalLine(approverOptions, finalApproverOptions, stepCount))
        {
            stepCounts.Add(stepCount);
        }
    }

    private bool canBuildApprovalLine(IReadOnlyList<ApprovalUserOption> approverOptions, IReadOnlyList<ApprovalUserOption> finalApproverOptions, int stepCount)
    {
        int requiredIntermediateApproverCount = stepCount - 1;
        foreach (ApprovalUserOption finalApproverOption in finalApproverOptions)
        {
            int availableIntermediateApproverCount = approverOptions.Where(option => option.UserID != finalApproverOption.UserID).Select(option => option.UserID).Distinct().Count();
            if (availableIntermediateApproverCount >= requiredIntermediateApproverCount)
            {
                return true;
            }
        }

        return false;
    }
}
