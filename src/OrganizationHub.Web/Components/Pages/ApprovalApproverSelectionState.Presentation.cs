using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

internal sealed partial class ApprovalApproverSelectionState
{
    public string GetApprovalStepLabel(ApprovalStepCount stepCount)
    {
        return ApprovalStepLabelUtility.GetStepOptionLabel(stepCount);
    }

    public string GetApprovalStepDescription()
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(mFormModel.StepCount);
        return ApprovalStepLabelUtility.GetStepDescription(stepCount);
    }
}
