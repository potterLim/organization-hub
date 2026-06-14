using System;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

internal sealed partial class ApprovalApproverSelectionState
{
    private readonly ApprovalCreateContext mCreateContext;
    private readonly ApprovalRequestFormModel mFormModel;
    private ApprovalUserID mLastDefaultFirstApproverUserID = ApprovalUserID.Empty;
    private ApprovalUserID mLastDefaultSecondApproverUserID = ApprovalUserID.Empty;
    private ApprovalUserID mLastDefaultFinalApproverUserID = ApprovalUserID.Empty;

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

    public void ChangeFirstApprover(ApprovalUserID userID)
    {
        mFormModel.FirstApproverUserID = userID.Value;
        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserID == userID.Value)
        {
            ApprovalUserID defaultSecondApproverUserID = getDefaultSecondApproverIDExcluding(
                new ApprovalUserID(mFormModel.FirstApproverUserID),
                new ApprovalUserID(mFormModel.FinalApproverUserID));
            mFormModel.SecondApproverUserID = defaultSecondApproverUserID.Value;
            mLastDefaultSecondApproverUserID = defaultSecondApproverUserID;
        }

        if (mFormModel.FinalApproverUserID == userID.Value)
        {
            ApprovalUserID defaultFinalApproverUserID = getDefaultFinalApproverIDExcluding(
                new ApprovalUserID(mFormModel.FirstApproverUserID),
                new ApprovalUserID(mFormModel.SecondApproverUserID));
            mFormModel.FinalApproverUserID = defaultFinalApproverUserID.Value;
            mLastDefaultFinalApproverUserID = defaultFinalApproverUserID;
        }
    }

    public void ChangeSecondApprover(ApprovalUserID userID)
    {
        mFormModel.SecondApproverUserID = userID.Value;
        if (mFormModel.FirstApproverUserID == userID.Value)
        {
            ApprovalUserID defaultFirstApproverUserID = getDefaultFirstApproverIDExcluding(
                new ApprovalUserID(mFormModel.SecondApproverUserID),
                new ApprovalUserID(mFormModel.FinalApproverUserID));
            mFormModel.FirstApproverUserID = defaultFirstApproverUserID.Value;
            mLastDefaultFirstApproverUserID = defaultFirstApproverUserID;
        }

        if (mFormModel.FinalApproverUserID == userID.Value)
        {
            ApprovalUserID defaultFinalApproverUserID = getDefaultFinalApproverIDExcluding(
                new ApprovalUserID(mFormModel.FirstApproverUserID),
                new ApprovalUserID(mFormModel.SecondApproverUserID));
            mFormModel.FinalApproverUserID = defaultFinalApproverUserID.Value;
            mLastDefaultFinalApproverUserID = defaultFinalApproverUserID;
        }
    }

    public void ChangeFinalApprover(ApprovalUserID userID)
    {
        mFormModel.FinalApproverUserID = userID.Value;
        if (mFormModel.FirstApproverUserID == userID.Value)
        {
            ApprovalUserID defaultFirstApproverUserID = getDefaultFirstApproverIDExcluding(
                new ApprovalUserID(mFormModel.SecondApproverUserID),
                new ApprovalUserID(mFormModel.FinalApproverUserID));
            mFormModel.FirstApproverUserID = defaultFirstApproverUserID.Value;
            mLastDefaultFirstApproverUserID = defaultFirstApproverUserID;
        }

        if (mFormModel.StepCount == 3 && mFormModel.SecondApproverUserID == userID.Value)
        {
            ApprovalUserID defaultSecondApproverUserID = getDefaultSecondApproverIDExcluding(
                new ApprovalUserID(mFormModel.FirstApproverUserID),
                new ApprovalUserID(mFormModel.FinalApproverUserID));
            mFormModel.SecondApproverUserID = defaultSecondApproverUserID.Value;
            mLastDefaultSecondApproverUserID = defaultSecondApproverUserID;
        }
    }
}
