using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private IReadOnlyList<ApprovalStepCount> createAvailableStepCounts(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions
    )
    {
        List<ApprovalStepCount> stepCounts = new List<ApprovalStepCount>();
        if (finalApproverOptions.Count == 0)
        {
            return stepCounts;
        }

        foreach (ApprovalStepCount stepCount in ApprovalStepCount.GetSupportedStepCounts())
        {
            addStepCountIfAvailable(stepCounts, approverOptions, finalApproverOptions, stepCount);
        }

        return stepCounts;
    }

    private void addStepCountIfAvailable(
        List<ApprovalStepCount> stepCounts,
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        ApprovalStepCount stepCount
    )
    {
        if (canBuildApprovalLine(approverOptions, finalApproverOptions, stepCount))
        {
            stepCounts.Add(stepCount);
        }
    }

    private bool canBuildApprovalLine(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        ApprovalStepCount stepCount
    )
    {
        int requiredIntermediateApproverCount = stepCount.GetIntermediateApproverCount();
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
