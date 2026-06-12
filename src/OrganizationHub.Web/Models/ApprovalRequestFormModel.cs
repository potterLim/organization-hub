namespace OrganizationHub.Web.Models;

public sealed class ApprovalRequestFormModel
{
    public string Title { get; set; } = string.Empty;
    public string SourceFolderID { get; set; } = string.Empty;
    public string DestinationFolderID { get; set; } = string.Empty;
    public string FileID { get; set; } = string.Empty;
    public int StepCount { get; set; } = 2;
    public int FirstApproverUserID
    {
        get; set;
    }
    public int SecondApproverUserID
    {
        get; set;
    }
    public int FinalApproverUserID
    {
        get; set;
    }
}
