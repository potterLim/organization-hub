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
            mFormModel.FirstApproverUserId = 0;
            mLastDefaultFirstApproverUserId = ApprovalUserId.Empty;
        }

        if (stepCount.RequiresSecondApprover == false)
        {
            mFormModel.SecondApproverUserId = 0;
            mLastDefaultSecondApproverUserId = ApprovalUserId.Empty;
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
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            mLastDefaultFirstApproverUserId,
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
        mFormModel.FirstApproverUserId = defaultFirstApproverUserId.GetOptionalValue();
        mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
    }

    private void applyDefaultSecondApproverIfNeeded(ApprovalStepCount stepCount)
    {
        if (stepCount.RequiresSecondApprover == false)
        {
            return;
        }

        bool shouldApplyDefault = shouldApplyDefaultApprover(
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
            mLastDefaultSecondApproverUserId,
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
        mFormModel.SecondApproverUserId = defaultSecondApproverUserId.GetOptionalValue();
        mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
    }

    private void applyDefaultFinalApproverIfNeeded()
    {
        bool shouldApplyDefault = shouldApplyDefaultFinalApprover(
            ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId),
            mLastDefaultFinalApproverUserId,
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId));
        if (shouldApplyDefault == false)
        {
            return;
        }

        ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
            ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
            ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId));
        mFormModel.FinalApproverUserId = defaultFinalApproverUserId.GetOptionalValue();
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
