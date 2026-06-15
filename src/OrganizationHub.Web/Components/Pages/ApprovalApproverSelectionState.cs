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
        mFormModel.StepCount = stepCount.GetRequiredValue();
        ApplyDefaultApproversForStepCount();
    }

    public void ChangeFirstApprover(ApprovalUserId userId)
    {
        int selectedUserId = userId.GetOptionalValue();
        mFormModel.FirstApproverUserId = selectedUserId;
        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserId == selectedUserId)
        {
            ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.SecondApproverUserId = defaultSecondApproverUserId.GetOptionalValue();
            mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
        }

        if (mFormModel.FinalApproverUserId == selectedUserId)
        {
            ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId));
            mFormModel.FinalApproverUserId = defaultFinalApproverUserId.GetOptionalValue();
            mLastDefaultFinalApproverUserId = defaultFinalApproverUserId;
        }
    }

    public void ChangeSecondApprover(ApprovalUserId userId)
    {
        int selectedUserId = userId.GetOptionalValue();
        mFormModel.SecondApproverUserId = selectedUserId;
        if (mFormModel.FirstApproverUserId == selectedUserId)
        {
            ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.FirstApproverUserId = defaultFirstApproverUserId.GetOptionalValue();
            mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
        }

        if (mFormModel.FinalApproverUserId == selectedUserId)
        {
            ApprovalUserId defaultFinalApproverUserId = getDefaultFinalApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId));
            mFormModel.FinalApproverUserId = defaultFinalApproverUserId.GetOptionalValue();
            mLastDefaultFinalApproverUserId = defaultFinalApproverUserId;
        }
    }

    public void ChangeFinalApprover(ApprovalUserId userId)
    {
        int selectedUserId = userId.GetOptionalValue();
        mFormModel.FinalApproverUserId = selectedUserId;
        if (mFormModel.FirstApproverUserId == selectedUserId)
        {
            ApprovalUserId defaultFirstApproverUserId = getDefaultFirstApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.SecondApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.FirstApproverUserId = defaultFirstApproverUserId.GetOptionalValue();
            mLastDefaultFirstApproverUserId = defaultFirstApproverUserId;
        }

        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserId == selectedUserId)
        {
            ApprovalUserId defaultSecondApproverUserId = getDefaultSecondApproverIdExcluding(
                ApprovalUserId.CreateOptional(mFormModel.FirstApproverUserId),
                ApprovalUserId.CreateOptional(mFormModel.FinalApproverUserId));
            mFormModel.SecondApproverUserId = defaultSecondApproverUserId.GetOptionalValue();
            mLastDefaultSecondApproverUserId = defaultSecondApproverUserId;
        }
    }
}
