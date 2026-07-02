using System.Linq;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

internal sealed partial class ApprovalApproverSelectionState
{
    public void ApplyDefaultApproversForStepCount()
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(mFormModel.StepCount);
        if (stepCount.RequiresFirstApprover() == false)
        {
            mFormModel.ClearFirstApprover();
            mLastDefaultFirstApproverUserId = ApprovalUserId.Empty;
        }

        if (stepCount.RequiresSecondApprover() == false)
        {
            mFormModel.ClearSecondApprover();
            mLastDefaultSecondApproverUserId = ApprovalUserId.Empty;
        }

        applyDefaultFirstApproverIfNeeded(stepCount);
        applyDefaultSecondApproverIfNeeded(stepCount);
        applyDefaultFinalApproverIfNeeded();
    }

    private void applyDefaultFirstApproverIfNeeded(ApprovalStepCount stepCount)
    {
        if (stepCount.RequiresFirstApprover() == false)
        {
            return;
        }

        bool shouldApplyDefault = shouldApplyDefaultApprover(
            mFormModel.FirstApproverUserIdValue,
            mLastDefaultFirstApproverUserId,
            mFormModel.SecondApproverUserIdValue,
            mFormModel.FinalApproverUserIdValue);
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
            mFormModel.SecondApproverUserIdValue,
            mFormModel.FinalApproverUserIdValue);
        mFormModel.FirstApproverUserIdValue = defaultFirstApproverUserId;
        mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
    }

    private void applyDefaultSecondApproverIfNeeded(ApprovalStepCount stepCount)
    {
        if (stepCount.RequiresSecondApprover() == false)
        {
            return;
        }

        bool shouldApplyDefault = shouldApplyDefaultApprover(
            mFormModel.SecondApproverUserIdValue,
            mLastDefaultSecondApproverUserId,
            mFormModel.FirstApproverUserIdValue,
            mFormModel.FinalApproverUserIdValue);
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
            mFormModel.FirstApproverUserIdValue,
            mFormModel.FinalApproverUserIdValue);
        mFormModel.SecondApproverUserIdValue = defaultSecondApproverUserId;
        mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
    }

    private void applyDefaultFinalApproverIfNeeded()
    {
        bool shouldApplyDefault = shouldApplyDefaultFinalApprover(
            mFormModel.FinalApproverUserIdValue,
            mLastDefaultFinalApproverUserId,
            mFormModel.FirstApproverUserIdValue,
            mFormModel.SecondApproverUserIdValue);
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
            mFormModel.FirstApproverUserIdValue,
            mFormModel.SecondApproverUserIdValue);
        mFormModel.FinalApproverUserIdValue = defaultFinalApproverUserId;
        mLastDefaultFinalApproverUserId = defaultFinalApproverUserId;
    }

    private bool shouldApplyDefaultApprover(ApprovalUserId selectedUserId, ApprovalUserId lastDefaultUserId, params ApprovalUserId[] excludedUserIds)
    {
        if (selectedUserId.HasValue == false || isApproverOptionSelected(selectedUserId) == false)
        {
            return true;
        }

        if (excludedUserIds.Any(userId => userId.HasValue && userId == selectedUserId))
        {
            return true;
        }

        return selectedUserId == lastDefaultUserId;
    }

    private bool shouldApplyDefaultFinalApprover(
        ApprovalUserId selectedUserId,
        ApprovalUserId lastDefaultUserId,
        params ApprovalUserId[] excludedUserIds
    )
    {
        if (selectedUserId.HasValue == false || isFinalApproverOptionSelected(selectedUserId) == false)
        {
            return true;
        }

        if (excludedUserIds.Any(userId => userId.HasValue && userId == selectedUserId))
        {
            return true;
        }

        return selectedUserId == lastDefaultUserId;
    }
}
