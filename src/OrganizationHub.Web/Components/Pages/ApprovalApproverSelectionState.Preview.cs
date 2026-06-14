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
            return "1차 결재 + 최종 결재";
        }

        if (stepCount.Value == 3)
        {
            return "1차 결재 + 2차 결재 + 최종 결재";
        }

        return stepCount.Value.ToString(CultureInfo.InvariantCulture) + "단계";
    }

    public string GetApprovalStepDescription()
    {
        if (mFormModel.StepCount == 3)
        {
            return "중간 결재자를 한 명 더 거친 뒤 최종 결재를 받습니다.";
        }

        return "1차 결재 후 최종 결재를 받습니다.";
    }

    public IReadOnlyList<ApprovalFlowPreviewItem> GetApprovalFlowPreviewItems()
    {
        List<ApprovalFlowPreviewItem> previewItems = new List<ApprovalFlowPreviewItem>();
        ApprovalStepCount stepCount = new ApprovalStepCount(mFormModel.StepCount);
        if (stepCount.RequiresFirstApprover)
        {
            previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, "1차 결재", ApprovalUserId.CreateExisting(mFormModel.FirstApproverUserId)));
        }

        if (stepCount.RequiresSecondApprover)
        {
            previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, "2차 결재", ApprovalUserId.CreateExisting(mFormModel.SecondApproverUserId)));
        }

        previewItems.Add(createApprovalFlowPreviewItem(previewItems.Count + 1, "최종 결재", ApprovalUserId.CreateExisting(mFormModel.FinalApproverUserId)));
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
            .FirstOrDefault(userOption => userOption.UserId == approverUserId.Value);
        if (userOptionOrNull == null)
        {
            return "선택 필요";
        }

        return userOptionOrNull.GetDisplayLabel();
    }
}
