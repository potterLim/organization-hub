namespace OrganizationHub.Web.Models;

public sealed class GlobalSettingFormModel
{
    public int ID
    {
        get; set;
    }
    public string OfficialRootFolderID { get; set; } = string.Empty;
    public string PublicFolderID { get; set; } = string.Empty;
    public string CommonFolderID { get; set; } = string.Empty;
    public string ResponsibleResourcesFolderID { get; set; } = string.Empty;
    public string WorkResourcesFolderID { get; set; } = string.Empty;
    public string OrganizationCalendarID { get; set; } = string.Empty;
    public string ResponsibleCalendarID { get; set; } = string.Empty;
    public string OrganizationTaskListID { get; set; } = string.Empty;
    public string ResponsibleTaskListID { get; set; } = string.Empty;
    public string NoticeFileID { get; set; } = string.Empty;
    public string WorkGuidelineFileID { get; set; } = string.Empty;
    public bool ShowTodoAuthor { get; set; } = true;
    public bool ShowCalendarAuthor { get; set; } = true;
}
