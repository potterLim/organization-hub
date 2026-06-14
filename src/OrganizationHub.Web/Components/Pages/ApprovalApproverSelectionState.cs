using System;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

internal sealed partial class ApprovalApproverSelectionState
{
    private readonly ApprovalCreateContext mCreateContext;
    private readonly ApprovalRequestFormModel mFormModel;
    private ApprovalUserId mLastDefaultFirstApproverUserId = ApprovalUserId.Empty;
    private ApprovalUserId mLastDefaultSecondApproverUserId = ApprovalUserId.Empty;
    private ApprovalUserId mLastDefaultFinalApproverUserId = ApprovalUserId.Empty;

    public ApprovalApproverSelectionState(ApprovalCreateContext createContext, ApprovalRequestFormModel formModel)
    {
        ArgumentNullException.ThrowIfNull(createContext);
        ArgumentNullException.ThrowIfNull(formModel);

        mCreateContext = createContext;
        mFormModel = formModel;
    }

    public void ChangeStepCount(ApprovalStepCount stepCount)
    {
        mFormModel.StepCount = stepCount.Value;
        ApplyDefaultApproversForStepCount();
    }

    public void ChangeFirstApprover(ApprovalUserId userId)
    {
        mFormModel.FirstApproverUserId = userId.Value;
        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserId == userId.Value)
        {
            ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.SecondApproverUserId = defaultSecondApproverUserId.Value;
            mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
        }

        if (mFormModel.FinalApproverUserId == userId.Value)
        {
            ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId));
            mFormModel.FinalApproverUserId = defaultFinalApproverUserId.Value;
            mLastDefaultFinalApproverUserId = defaultFinalApproverUserId;
        }
    }

    public void ChangeSecondApprover(ApprovalUserId userId)
    {
        mFormModel.SecondApproverUserId = userId.Value;
        if (mFormModel.FirstApproverUserId == userId.Value)
        {
            ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.FirstApproverUserId = defaultFirstApproverUserId.Value;
            mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
        }

        if (mFormModel.FinalApproverUserId == userId.Value)
        {
            ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId));
            mFormModel.FinalApproverUserId = defaultFinalApproverUserId.Value;
            mLastDefaultFinalApproverUserId = defaultFinalApproverUserId;
        }
    }

    public void ChangeFinalApprover(ApprovalUserId userId)
    {
        mFormModel.FinalApproverUserId = userId.Value;
        if (mFormModel.FirstApproverUserId == userId.Value)
        {
            ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.FirstApproverUserId = defaultFirstApproverUserId.Value;
            mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
        }

        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserId == userId.Value)
        {
            ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.SecondApproverUserId = defaultSecondApproverUserId.Value;
            mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
        }
    }
}
