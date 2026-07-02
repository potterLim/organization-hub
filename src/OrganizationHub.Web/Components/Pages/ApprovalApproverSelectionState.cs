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
        mFormModel.StepCountValue = new ApprovalStepCountSelection(stepCount.GetRequiredValue());
        ApplyDefaultApproversForStepCount();
    }

    public void ChangeFirstApprover(ApprovalUserId userId)
    {
        mFormModel.FirstApproverUserIdValue = userId;
        if (mFormModel.StepCountValue.RequiresSecondApprover() && mFormModel.SecondApproverUserIdValue == userId)
        {
            ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
                mFormModel.FirstApproverUserIdValue,
                mFormModel.FinalApproverUserIdValue);
            mFormModel.SecondApproverUserIdValue = defaultSecondApproverUserId;
            mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
        }

        if (mFormModel.FinalApproverUserIdValue == userId)
        {
            ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
                mFormModel.FirstApproverUserIdValue,
                mFormModel.SecondApproverUserIdValue);
            mFormModel.FinalApproverUserIdValue = defaultFinalApproverUserId;
            mLastDefaultFinalApproverUserId = defaultFinalApproverUserId;
        }
    }

    public void ChangeSecondApprover(ApprovalUserId userId)
    {
        mFormModel.SecondApproverUserIdValue = userId;
        if (mFormModel.FirstApproverUserIdValue == userId)
        {
            ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
                mFormModel.SecondApproverUserIdValue,
                mFormModel.FinalApproverUserIdValue);
            mFormModel.FirstApproverUserIdValue = defaultFirstApproverUserId;
            mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
        }

        if (mFormModel.FinalApproverUserIdValue == userId)
        {
            ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
                mFormModel.FirstApproverUserIdValue,
                mFormModel.SecondApproverUserIdValue);
            mFormModel.FinalApproverUserIdValue = defaultFinalApproverUserId;
            mLastDefaultFinalApproverUserId = defaultFinalApproverUserId;
        }
    }

    public void ChangeFinalApprover(ApprovalUserId userId)
    {
        mFormModel.FinalApproverUserIdValue = userId;
        if (mFormModel.FirstApproverUserIdValue == userId)
        {
            ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
                mFormModel.SecondApproverUserIdValue,
                mFormModel.FinalApproverUserIdValue);
            mFormModel.FirstApproverUserIdValue = defaultFirstApproverUserId;
            mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
        }

        if (mFormModel.StepCountValue.RequiresSecondApprover() && mFormModel.SecondApproverUserIdValue == userId)
        {
            ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
                mFormModel.FirstApproverUserIdValue,
                mFormModel.FinalApproverUserIdValue);
            mFormModel.SecondApproverUserIdValue = defaultSecondApproverUserId;
            mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
        }
    }
}
