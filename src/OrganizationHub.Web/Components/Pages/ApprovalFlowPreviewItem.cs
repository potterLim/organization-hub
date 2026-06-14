namespace OrganizationHub.Web.Components.Pages;

public sealed class ApprovalFlowPreviewItem
{
    public string StepNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ApproverLabel { get; set; } = string.Empty;
    public bool IsComplete
    {
        get; set;
    }
}
