using System.Linq;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

internal sealed partial class ApprovalApproverSelectionState
{
    public void ApplyDefaultApproversForStepCount()
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(mFormModel.StepCount);
        if (stepCount.RequiresFirstApprover == false)
        {
            mFormModel.FirstApproverUserID = 0;
            mLastDefaultFirstApproverUserID = ApprovalUserID.Empty;
        }

        if (stepCount.RequiresSecondApprover == false)
        {
            mFormModel.SecondApproverUserID = 0;
            mLastDefaultSecondApproverUserID = ApprovalUserID.Empty;
        }

        applyDefaultFirstApproverIfNeeded(stepCount);
        applyDefaultSecondApproverIfNeeded(stepCount);
        applyDefaultFinalApproverIfNeeded();
    }

    private void applyDefaultFirstApproverIfNeeded(ApprovalStepCount stepCount)
    {
        if (stepCount.RequiresFirstApprover == false)
        {
            return;
        }

        bool shouldApplyDefault = shouldApplyDefaultApprover(
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            mLastDefaultFirstApproverUserID,
            new ApprovalUserID(mFormModel.SecondApproverUserID),
            new ApprovalUserID(mFormModel.FinalApproverUserID));
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserID defaultFirstApproverUserID = getDefaultFirstApproverIDExcluding(
            new ApprovalUserID(mFormModel.SecondApproverUserID),
            new ApprovalUserID(mFormModel.FinalApproverUserID));
        mFormModel.FirstApproverUserID = defaultFirstApproverUserID.Value;
        mLastDefaultFirstApproverUserID = defaultFirstApproverUserID;
    }

    private void applyDefaultSecondApproverIfNeeded(ApprovalStepCount stepCount)
    {
        if (stepCount.RequiresSecondApprover == false)
        {
            return;
        }

        bool shouldApplyDefault = shouldApplyDefaultApprover(
            new ApprovalUserID(mFormModel.SecondApproverUserID),
            mLastDefaultSecondApproverUserID,
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            new ApprovalUserID(mFormModel.FinalApproverUserID));
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserID defaultSecondApproverUserID = getDefaultSecondApproverIDExcluding(
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            new ApprovalUserID(mFormModel.FinalApproverUserID));
        mFormModel.SecondApproverUserID = defaultSecondApproverUserID.Value;
        mLastDefaultSecondApproverUserID = defaultSecondApproverUserID;
    }

    private void applyDefaultFinalApproverIfNeeded()
    {
        bool shouldApplyDefault = shouldApplyDefaultFinalApprover(
            new ApprovalUserID(mFormModel.FinalApproverUserID),
            mLastDefaultFinalApproverUserID,
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            new ApprovalUserID(mFormModel.SecondApproverUserID));
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserID defaultFinalApproverUserID = getDefaultFinalApproverIDExcluding(
            new ApprovalUserID(mFormModel.FirstApproverUserID),
            new ApprovalUserID(mFormModel.SecondApproverUserID));
        mFormModel.FinalApproverUserID = defaultFinalApproverUserID.Value;
        mLastDefaultFinalApproverUserID = defaultFinalApproverUserID;
    }

    private bool shouldApplyDefaultApprover(ApprovalUserID selectedUserID, ApprovalUserID lastDefaultUserID, params ApprovalUserID[] excludedUserIDs)
    {
        if (selectedUserID.HasValue == false || isApproverOptionSelected(selectedUserID) == false)
        {
            return true;
        }

        if (excludedUserIDs.Any(userID => userID.HasValue && userID == selectedUserID))
        {
            return true;
        }

        return selectedUserID == lastDefaultUserID;
    }

    private bool shouldApplyDefaultFinalApprover(ApprovalUserID selectedUserID, ApprovalUserID lastDefaultUserID, params ApprovalUserID[] excludedUserIDs)
    {
        if (selectedUserID.HasValue == false || isFinalApproverOptionSelected(selectedUserID) == false)
        {
            return true;
        }

        if (excludedUserIDs.Any(userID => userID.HasValue && userID == selectedUserID))
        {
            return true;
        }

        return selectedUserID == lastDefaultUserID;
    }
}
