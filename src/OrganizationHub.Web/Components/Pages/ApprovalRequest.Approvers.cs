using System.Collections.Generic;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalRequest
{
    private void initializeApprovalRequestDefaults()
    {
        mApproverSelectionStateOrNull = new ApprovalApproverSelectionState(mCreateContext, mFormModel);

        if (mCreateContext.SourceFolders.Count > 0)
        {
            mFormModel.SourceFolderId = mCreateContext.SourceFolders[0].FolderId.GetRequiredValue();
            resetSourcePath(mCreateContext.SourceFolders[0]);
        }

        if (mCreateContext.DestinationFolders.Count > 0)
        {
            mSelectedDestinationRootFolderId = mCreateContext.DestinationFolders[0].FolderId.GetRequiredValue();
            resetDestinationPath(mCreateContext.DestinationFolders[0]);
        }

        if (mCreateContext.AvailableStepCounts.Count > 0)
        {
            mFormModel.StepCount = mCreateContext.AvailableStepCounts[0];
        }

        getApproverSelectionState().ApplyDefaultApproversForStepCount();
    }

    private Task handleStepCountChangedAsync(int stepCount)
    {
        getApproverSelectionState().ChangeStepCount(new ApprovalStepCount(stepCount));
        return Task.CompletedTask;
    }

    private Task handleFirstApproverChangedAsync(int userId)
    {
        getApproverSelectionState().ChangeFirstApprover(ApprovalUserId.CreateExisting(userId));
        return Task.CompletedTask;
    }

    private Task handleSecondApproverChangedAsync(int userId)
    {
        getApproverSelectionState().ChangeSecondApprover(ApprovalUserId.CreateExisting(userId));
        return Task.CompletedTask;
    }

    private Task handleFinalApproverChangedAsync(int userId)
    {
        getApproverSelectionState().ChangeFinalApprover(ApprovalUserId.CreateExisting(userId));
        return Task.CompletedTask;
    }

    private Task handleTitleChangedAsync(string title)
    {
        mFormModel.Title = title;
        mHasCustomTitle = true;
        return Task.CompletedTask;
    }

    private IReadOnlyList<ApprovalUserOption> getFirstApproverOptions()
    {
        return getApproverSelectionState().GetFirstApproverOptions();
    }

    private IReadOnlyList<ApprovalUserOption> getSecondApproverOptions()
    {
        return getApproverSelectionState().GetSecondApproverOptions();
    }

    private IReadOnlyList<ApprovalUserOption> getFinalApproverOptions()
    {
        return getApproverSelectionState().GetFinalApproverOptions();
    }

    private string getApprovalStepLabel(int stepCount)
    {
        return getApproverSelectionState().GetApprovalStepLabel(new ApprovalStepCount(stepCount));
    }

    private string getApprovalStepDescription()
    {
        return getApproverSelectionState().GetApprovalStepDescription();
    }

    private IReadOnlyList<ApprovalFlowPreviewItem> getApprovalFlowPreviewItems()
    {
        return getApproverSelectionState().GetApprovalFlowPreviewItems();
    }

    private string getApprovalFlowStepClass(ApprovalFlowPreviewItem previewItem)
    {
        return getApproverSelectionState().GetApprovalFlowStepClass(previewItem);
    }

    private ApprovalApproverSelectionState getApproverSelectionState()
    {
        if (mApproverSelectionStateOrNull == null)
        {
            mApproverSelectionStateOrNull = new ApprovalApproverSelectionState(mCreateContext, mFormModel);
        }

        return mApproverSelectionStateOrNull;
    }
}
