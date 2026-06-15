using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

internal sealed partial class ApprovalApproverSelectionState
{
    public string GetApprovalStepLabel(ApprovalStepCount stepCount)
    {
        if (stepCount.Value == 2)
        {
            return ApprovalStepLabelUtility.GetStepOptionLabel(stepCount);
        }

        if (stepCount.Value == 3)
        {
            return ApprovalStepLabelUtility.GetStepOptionLabel(stepCount);
        }

        return ApprovalStepLabelUtility.GetStepOptionLabel(stepCount);
    }

    public string GetApprovalStepDescription()
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(mFormModel.StepCount);
        return ApprovalStepLabelUtility.GetStepDescription(stepCount);
    }

    public IReadOnlyList<ApprovalFlowPreviewItem> GetApprovalFlowPreviewItems()
    {
        List<ApprovalFlowPreviewItem> previewItems = new List<ApprovalFlowPreviewItem>();
        ApprovalStepCount stepCount = new ApprovalStepCount(mFormModel.StepCount);
        if (stepCount.RequiresFirstApprover)
        {
            string stepRoleName = ApprovalStepLabelUtility.GetStepRoleName(stepCount, previewItems.Count + 1);
            previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, stepRoleName, ApprovalUserId.CreateExisting(mFormModel.FirstApproverUserId)));
        }

        if (stepCount.RequiresSecondApprover)
        {
            string stepRoleName = ApprovalStepLabelUtility.GetStepRoleName(stepCount, previewItems.Count + 1);
            previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, stepRoleName, ApprovalUserId.CreateExisting(mFormModel.SecondApproverUserId)));
        }

        string finalStepRoleName = ApprovalStepLabelUtility.GetStepRoleName(stepCount, previewItems.Count + 1);
        previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, finalStepRoleName, ApprovalUserId.CreateExisting(mFormModel.FinalApproverUserId)));
        return previewItems;
    }

    public string GetApprovalFlowStepClass(ApprovalFlowPreviewItem previewItem)
    {
        if (previewItem.IsComplete)
        {
            return "hub-approval-flow-step";
        }

        return "hub-approval-flow-step hub-approval-flow-step-empty";
    }

    private ApprovalFlowPreviewItem createApprovalFlowPreviewItem(int stepNumber, string title, ApprovalUserId approverUserId)
    {
        ApprovalFlowPreviewItem previewItem = new ApprovalFlowPreviewItem();
        previewItem.StepNumber = stepNumber.ToString(CultureInfo.InvariantCulture);
        previewItem.Title = title;
        previewItem.ApproverLabel = getApproverLabelOrPlaceholder(approverUserId);
        previewItem.IsComplete = approverUserId.HasValue;
        return previewItem;
    }

    private string getApproverLabelOrPlaceholder(ApprovalUserId approverUserId)
    {
        if (approverUserId.HasValue == false)
        {
            return "선택 필요";
        }

        ApprovalUserOption? userOptionOrNull = mCreateContext.ApproverOptions
            .Concat(mCreateContext.FinalApproverOptions)
            .FirstOrDefault(userOption => userOption.UserId == approverUserId.GetRequiredValue());
        if (userOptionOrNull == null)
        {
            return "선택 필요";
        }

        return userOptionOrNull.GetDisplayLabel();
    }
}
