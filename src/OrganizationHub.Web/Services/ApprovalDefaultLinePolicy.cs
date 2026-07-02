using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public static class ApprovalDefaultLinePolicy
{
    public static IReadOnlyList<ApprovalDefaultLineOption> CreateDefaultLineOptions(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        IReadOnlyList<ApprovalStepCount> availableStepCounts)
    {
        List<ApprovalDefaultLineOption> defaultLineOptions = new List<ApprovalDefaultLineOption>();
        foreach (ApprovalStepCount approvalStepCount in availableStepCounts)
        {
            ApprovalDefaultLineOption defaultLineOption = createDefaultLineOption(approverOptions, finalApproverOptions, approvalStepCount);
            defaultLineOptions.Add(defaultLineOption);
        }

        return defaultLineOptions;
    }

    private static ApprovalDefaultLineOption createDefaultLineOption(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        ApprovalStepCount stepCount)
    {
        HashSet<ApprovalUserId> selectedUserIds = new HashSet<ApprovalUserId>();
        ApprovalUserId finalApproverUserId = getFinalApproverUserId(approverOptions, finalApproverOptions, stepCount);
        addSelectedUserId(selectedUserIds, finalApproverUserId);

        ApprovalDefaultLineOption defaultLineOption = new ApprovalDefaultLineOption();
        defaultLineOption.StepCount = stepCount;
        defaultLineOption.FinalApproverUserIdValue = finalApproverUserId;
        if (stepCount.RequiresFirstApprover())
        {
            ApprovalUserId firstApproverUserId = getIntermediateApproverUserId(approverOptions, selectedUserIds);
            defaultLineOption.FirstApproverUserIdValue = firstApproverUserId;
            addSelectedUserId(selectedUserIds, firstApproverUserId);
        }

        if (stepCount.RequiresSecondApprover())
        {
            ApprovalUserId secondApproverUserId = getIntermediateApproverUserId(approverOptions, selectedUserIds);
            defaultLineOption.SecondApproverUserIdValue = secondApproverUserId;
        }

        return defaultLineOption;
    }

    private static ApprovalUserId getFinalApproverUserId(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        ApprovalStepCount stepCount)
    {
        ApprovalUserOption? finalApproverOrNull = finalApproverOptions.FirstOrDefault(finalApprover => canCompleteLineWithFinalApprover(approverOptions, finalApprover, stepCount));
        if (finalApproverOrNull == null)
        {
            return ApprovalUserId.Empty;
        }

        return ApprovalUserId.CreateExisting(finalApproverOrNull.UserId);
    }

    private static ApprovalUserId getIntermediateApproverUserId(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        HashSet<ApprovalUserId> selectedUserIds)
    {
        ApprovalUserOption? approverOrNull = approverOptions.FirstOrDefault(option => selectedUserIds.Contains(ApprovalUserId.CreateExisting(option.UserId)) == false);
        if (approverOrNull == null)
        {
            return ApprovalUserId.Empty;
        }

        return ApprovalUserId.CreateExisting(approverOrNull.UserId);
    }

    private static bool canCompleteLineWithFinalApprover(
        IReadOnlyList<ApprovalUserOption> approverOptions,
        ApprovalUserOption finalApproverOption,
        ApprovalStepCount stepCount)
    {
        int requiredIntermediateApproverCount = stepCount.GetIntermediateApproverCount();
        int availableIntermediateApproverCount = approverOptions
            .Where(option => option.UserId != finalApproverOption.UserId)
            .Select(option => ApprovalUserId.CreateExisting(option.UserId))
            .Distinct()
            .Count();
        return availableIntermediateApproverCount >= requiredIntermediateApproverCount;
    }

    private static void addSelectedUserId(HashSet<ApprovalUserId> selectedUserIds, ApprovalUserId userId)
    {
        if (userId.HasValue)
        {
            selectedUserIds.Add(userId);
        }
    }
}
