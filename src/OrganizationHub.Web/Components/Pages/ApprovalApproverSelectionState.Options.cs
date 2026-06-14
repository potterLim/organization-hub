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
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            new ApprovalUserID(mFormModel.SecondApproverUserID),
            new ApprovalUserID(mFormModel.FinalApproverUserID));
    }

    public IReadOnlyList<ApprovalUserOption> GetSecondApproverOptions()
    {
        return getApproverOptionsExcludingExcept(
            new ApprovalUserID(mFormModel.SecondApproverUserID),
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            new ApprovalUserID(mFormModel.FinalApproverUserID));
    }

    public IReadOnlyList<ApprovalUserOption> GetFinalApproverOptions()
    {
        return getFinalApproverOptionsExcludingExcept(
            new ApprovalUserID(mFormModel.FinalApproverUserID),
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            new ApprovalUserID(mFormModel.SecondApproverUserID));
    }

    private IReadOnlyList<ApprovalUserOption> getApproverOptionsExcludingExcept(ApprovalUserID currentUserID, params ApprovalUserID[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = createExcludedUserIDSet(currentUserID, excludedUserIDs);
        return mCreateContext.ApproverOptions.Where(userOption => excludedIDs.Contains(userOption.UserID) == false).ToList();
    }

    private IReadOnlyList<ApprovalUserOption> getFinalApproverOptionsExcludingExcept(ApprovalUserID currentUserID, params ApprovalUserID[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = createExcludedUserIDSet(currentUserID, excludedUserIDs);
        return mCreateContext.FinalApproverOptions.Where(userOption => excludedIDs.Contains(userOption.UserID) == false).ToList();
    }

    private HashSet<int> createExcludedUserIDSet(ApprovalUserID currentUserID, IReadOnlyList<ApprovalUserID> excludedUserIDs)
    {
        return excludedUserIDs
            .Where(userID => userID.HasValue && userID != currentUserID)
            .Select(userID => userID.Value)
            .ToHashSet();
    }

    private ApprovalUserID getDefaultFirstApproverIDExcluding(params ApprovalUserID[] excludedUserIDs)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        ApprovalUserID preferredUserID = defaultLineOrNull == null ? ApprovalUserID.Empty : new ApprovalUserID(defaultLineOrNull.FirstApproverUserID);
        return getPreferredApproverIDExcluding(preferredUserID, excludedUserIDs);
    }

    private ApprovalUserID getDefaultSecondApproverIDExcluding(params ApprovalUserID[] excludedUserIDs)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        ApprovalUserID preferredUserID = defaultLineOrNull == null ? ApprovalUserID.Empty : new ApprovalUserID(defaultLineOrNull.SecondApproverUserID);
        return getPreferredApproverIDExcluding(preferredUserID, excludedUserIDs);
    }

    private ApprovalUserID getDefaultFinalApproverIDExcluding(params ApprovalUserID[] excludedUserIDs)
    {
        ApprovalDefaultLineOption? defaultLineOrNull = getDefaultLineOrNull();
        ApprovalUserID preferredUserID = defaultLineOrNull == null ? ApprovalUserID.Empty : new ApprovalUserID(defaultLineOrNull.FinalApproverUserID);
        return getPreferredFinalApproverIDExcluding(preferredUserID, excludedUserIDs);
    }

    private ApprovalUserID getPreferredApproverIDExcluding(ApprovalUserID preferredUserID, params ApprovalUserID[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = createExcludedUserIDSet(ApprovalUserID.Empty, excludedUserIDs);
        if (preferredUserID.HasValue && excludedIDs.Contains(preferredUserID.Value) == false && isApproverOptionSelected(preferredUserID))
        {
            return preferredUserID;
        }

        return getFirstApproverIDExcluding(excludedUserIDs);
    }

    private ApprovalUserID getPreferredFinalApproverIDExcluding(ApprovalUserID preferredUserID, params ApprovalUserID[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = createExcludedUserIDSet(ApprovalUserID.Empty, excludedUserIDs);
        if (preferredUserID.HasValue && excludedIDs.Contains(preferredUserID.Value) == false && isFinalApproverOptionSelected(preferredUserID))
        {
            return preferredUserID;
        }

        return getFirstFinalApproverIDExcluding(excludedUserIDs);
    }

    private ApprovalUserID getFirstApproverIDExcluding(params ApprovalUserID[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = createExcludedUserIDSet(ApprovalUserID.Empty, excludedUserIDs);
        ApprovalUserOption? approverOptionOrNull = mCreateContext.ApproverOptions.FirstOrDefault(userOption => excludedIDs.Contains(userOption.UserID) == false);
        if (approverOptionOrNull == null)
        {
            return ApprovalUserID.Empty;
        }

        return new ApprovalUserID(approverOptionOrNull.UserID);
    }

    private ApprovalUserID getFirstFinalApproverIDExcluding(params ApprovalUserID[] excludedUserIDs)
    {
        HashSet<int> excludedIDs = createExcludedUserIDSet(ApprovalUserID.Empty, excludedUserIDs);
        ApprovalUserOption? finalApproverOptionOrNull = mCreateContext.FinalApproverOptions.FirstOrDefault(userOption => excludedIDs.Contains(userOption.UserID) == false);
        if (finalApproverOptionOrNull == null)
        {
            return ApprovalUserID.Empty;
        }

        return new ApprovalUserID(finalApproverOptionOrNull.UserID);
    }

    private ApprovalDefaultLineOption? getDefaultLineOrNull()
    {
        return mCreateContext.DefaultLineOptions.FirstOrDefault(defaultLine => defaultLine.StepCount == mFormModel.StepCount);
    }

    private bool isApproverOptionSelected(ApprovalUserID userID)
    {
        return userID.HasValue && mCreateContext.ApproverOptions.Any(userOption => userOption.UserID == userID.Value);
    }

    private bool isFinalApproverOptionSelected(ApprovalUserID userID)
    {
        return userID.HasValue && mCreateContext.FinalApproverOptions.Any(userOption => userOption.UserID == userID.Value);
    }
}
