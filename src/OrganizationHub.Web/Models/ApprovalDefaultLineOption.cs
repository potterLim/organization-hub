namespace OrganizationHub.Web.Models;

public sealed class ApprovalDefaultLineOption
{
    public int StepCount
    {
        get; set;
    }
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
