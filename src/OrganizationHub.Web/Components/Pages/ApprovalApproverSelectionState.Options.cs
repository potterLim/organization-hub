using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

internal sealed partial class ApprovalApproverSelectionState
{
    public IReadOnlyList<ApprovalUserOption> GetFirstApproverOptions()
    {
        return getApproverOptionsExcludingExcept(
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
    }

    public IReadOnlyList<ApprovalUserOption> GetSecondApproverOptions()
    {
        return getApproverOptionsExcludingExcept(
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
    }

    public IReadOnlyList<ApprovalUserOption> GetFinalApproverOptions()
    {
        return getFinalApproverOptionsExcludingExcept(
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId));
    }

    private IReadOnlyList<ApprovalUserOption> getApproverOptionsExcludingExcept(ApprovalUserId currentUserId, params ApprovalUserId[] excludedUserIds)
    {
        HashSet<int> excludedIds = createExcludedUserIdSet(currentUserId, excludedUserIds);
        return mCreateContext.ApproverOptions.Where(userOption => excludedIds.Contains(userOption.UserId) == false).ToList();
    }

    private IReadOnlyList<ApprovalUserOption> getFinalApproverOptionsExcludingExcept(
        ApprovalUserId currentUserId,
        params ApprovalUserId[] excludedUserIds
    )
    {
        HashSet<int> excludedIds = createExcludedUserIdSet(currentUserId, excludedUserIds);
        return mCreateContext.FinalApproverOptions.Where(userOption => excludedIds.Contains(userOption.UserId) == false).ToList();
    }

    private HashSet<int> createExcludedUserIdSet(ApprovalUserId currentUserId, IReadOnlyList<ApprovalUserId> excludedUserIds)
    {
        return excludedUserIds
            .Where(userId => userId.HasValue && userId != currentUserId)
            .Select(userId => userId.GetRequiredValue())
            .ToHashSet();
    }

    private ApprovalUserId getDefaultFirstApproverIdExcluding(params ApprovalUserId[] excludedUserIds)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        ApprovalUserId preferredUserId = ApprovalUserId.Empty;
        if (defaultLineOrNull != null)
        {
            preferredUserId = ApprovalUserId.CreateExisting(defaultLineOrNull.FirstApproverUserId);
        }

        return getPreferredApproverIdExcluding(preferredUserId, excludedUserIds);
    }

    private ApprovalUserId getDefaultSecondApproverIdExcluding(params ApprovalUserId[] excludedUserIds)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        ApprovalUserId preferredUserId = ApprovalUserId.Empty;
        if (defaultLineOrNull != null)
        {
            preferredUserId = ApprovalUserId.CreateExisting(defaultLineOrNull.SecondApproverUserId);
        }

        return getPreferredApproverIdExcluding(preferredUserId, excludedUserIds);
    }

    private ApprovalUserId getDefaultFinalApproverIdExcluding(params ApprovalUserId[] excludedUserIds)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        ApprovalUserId preferredUserId = ApprovalUserId.Empty;
        if (defaultLineOrNull != null)
        {
            preferredUserId = ApprovalUserId.CreateExisting(defaultLineOrNull.FinalApproverUserId);
        }

        return getPreferredFinalApproverIdExcluding(preferredUserId, excludedUserIds);
    }

    private ApprovalUserId getPreferredApproverIdExcluding(ApprovalUserId preferredUserId, params ApprovalUserId[] excludedUserIds)
    {
        HashSet<int> excludedIds = createExcludedUserIdSet(ApprovalUserId.Empty, excludedUserIds);
        if (preferredUserId.HasValue && excludedIds.Contains(preferredUserId.GetRequiredValue()) == false && isApproverOptionSelected(preferredUserId))
        {
            return preferredUserId;
        }

        return getFirstApproverIdExcluding(excludedUserIds);
    }

    private ApprovalUserId getPreferredFinalApproverIdExcluding(ApprovalUserId preferredUserId, params ApprovalUserId[] excludedUserIds)
    {
        HashSet<int> excludedIds = createExcludedUserIdSet(ApprovalUserId.Empty, excludedUserIds);
        if (preferredUserId.HasValue && excludedIds.Contains(preferredUserId.GetRequiredValue()) == false && isFinalApproverOptionSelected(preferredUserId))
        {
            return preferredUserId;
        }

        return getFirstFinalApproverIdExcluding(excludedUserIds);
    }

    private ApprovalUserId getFirstApproverIdExcluding(params ApprovalUserId[] excludedUserIds)
    {
        HashSet<int> excludedIds = createExcludedUserIdSet(ApprovalUserId.Empty, excludedUserIds);
        ApprovalUserOption? approverOptionOrNull = mCreateContext.ApproverOptions.FirstOrDefault(userOption => excludedIds.Contains(userOption.UserId) == false);
        if (approverOptionOrNull == null)
        {
            return ApprovalUserId.Empty;
        }

        return ApprovalUserId.CreateExisting(approverOptionOrNull.UserId);
    }

    private ApprovalUserId getFirstFinalApproverIdExcluding(params ApprovalUserId[] excludedUserIds)
    {
        HashSet<int> excludedIds = createExcludedUserIdSet(ApprovalUserId.Empty, excludedUserIds);
        ApprovalUserOption? finalApproverOptionOrNull = mCreateContext.FinalApproverOptions.FirstOrDefault(userOption => excludedIds.Contains(userOption.UserId) == false);
        if (finalApproverOptionOrNull == null)
        {
            return ApprovalUserId.Empty;
        }

        return ApprovalUserId.CreateExisting(finalApproverOptionOrNull.UserId);
    }

    private ApprovalDefaultLineOption? getDefaultLineOrNull()
    {
        return mCreateContext.DefaultLineOptions.FirstOrDefault(defaultLine => defaultLine.StepCount == mFormModel.StepCount);
    }

    private bool isApproverOptionSelected(ApprovalUserId userId)
    {
        return userId.HasValue && mCreateContext.ApproverOptions.Any(userOption => userOption.UserId == userId.GetRequiredValue());
    }

    private bool isFinalApproverOptionSelected(ApprovalUserId userId)
    {
        return userId.HasValue && mCreateContext.FinalApproverOptions.Any(userOption => userOption.UserId == userId.GetRequiredValue());
    }
}
