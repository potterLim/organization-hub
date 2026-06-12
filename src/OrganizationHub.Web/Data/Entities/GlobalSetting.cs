namespace OrganizationHub.Web.Data.Entities;

public class GlobalSetting
{
    public int ID
    {
        get; set;
    }
    public string? OfficialRootFolderID
    {
        get; set;
    }
    public string? PublicFolderID
    {
        get; set;
    }
    public string? CommonFolderID
    {
        get; set;
    }
    public string? ResponsibleResourcesFolderID
    {
        get; set;
    }
    public string? WorkResourcesFolderID
    {
        get; set;
    }
    public string? ArchiveFolderID
    {
        get; set;
    }
    public string? OrganizationCalendarID
    {
        get; set;
    }
    public string? ResponsibleCalendarID
    {
        get; set;
    }
    public string? OrganizationTaskListID
    {
        get; set;
    }
    public string? ResponsibleTaskListID
    {
        get; set;
    }
    public string? NoticeFileID
    {
        get; set;
    }
    public string? WorkGuidelineFileID
    {
        get; set;
    }
    public bool ShowTodoAuthor { get; set; } = true;
    public bool ShowCalendarAuthor { get; set; } = true;
}
