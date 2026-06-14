using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public static partial class ApprovalDefaultLinePolicy
{
    public static IReadOnlyList<ApprovalDefaultLineOption> CreateDefaultLineOptions(
        UserSession userSession,
        IReadOnlyList<ApprovalUserOption> approverOptions,
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        IReadOnlyList<int> availableStepCounts)
    {
        List<ApprovalDefaultLineOption> defaultLineOptions = new List<ApprovalDefaultLineOption>();
        foreach (int stepCount in availableStepCounts)
        {
            ApprovalStepCount approvalStepCount = new ApprovalStepCount(stepCount);
            HashSet<ApprovalUserId> selectedUserIds = new HashSet<ApprovalUserId>();
            ApprovalUserId finalApproverUserId = getPreferredFinalApproverId(
                userSession,
                approverOptions,
                finalApproverOptions,
                selectedUserIds,
                approvalStepCount);
            addSelectedUserId(selectedUserIds, finalApproverUserId);

            ApprovalUserId firstApproverUserId = ApprovalUserId.Empty;
            if (approvalStepCount.RequiresFirstApprover)
            {
                firstApproverUserId = getPreferredIntermediateApproverId(
                    userSession,
                    approvalStepCount,
                    EApprovalIntermediateApproverSlot.First,
                    approverOptions,
                    selectedUserIds);
                addSelectedUserId(selectedUserIds, firstApproverUserId);
            }

            ApprovalUserId secondApproverUserId = ApprovalUserId.Empty;
            if (approvalStepCount.RequiresSecondApprover)
            {
                secondApproverUserId = getPreferredIntermediateApproverId(
                    userSession,
                    approvalStepCount,
                    EApprovalIntermediateApproverSlot.Second,
                    approverOptions,
                    selectedUserIds);
                addSelectedUserId(selectedUserIds, secondApproverUserId);
            }

            ApprovalDefaultLineOption defaultLineOption = new ApprovalDefaultLineOption();
            defaultLineOption.StepCount = approvalStepCount.Value;
            defaultLineOption.FirstApproverUserId = firstApproverUserId.Value;
            defaultLineOption.SecondApproverUserId = secondApproverUserId.Value;
            defaultLineOption.FinalApproverUserId = finalApproverUserId.Value;
            defaultLineOptions.Add(defaultLineOption);
        }

        return defaultLineOptions;
    }

    private static void addSelectedUserId(HashSet<ApprovalUserId> selectedUserIds, ApprovalUserId userId)
    {
        if (userId.HasValue)
        {
            selectedUserIds.Add(userId);
        }
    }
}
