namespace OrganizationHub.Web.Data.Entities;

public class GlobalSetting
{
    public int Id
    {
        get; set;
    }
    public string? OfficialRootFolderId
    {
        get; set;
    }
    public string? PublicFolderId
    {
        get; set;
    }
    public string? CommonFolderId
    {
        get; set;
    }
    public string? ResponsibleResourcesFolderId
    {
        get; set;
    }
    public string? WorkResourcesFolderId
    {
        get; set;
    }
    public string? ArchiveFolderId
    {
        get; set;
    }
    public string? OrganizationCalendarId
    {
        get; set;
    }
    public string? ResponsibleCalendarId
    {
        get; set;
    }
    public string? OrganizationTaskListId
    {
        get; set;
    }
    public string? ResponsibleTaskListId
    {
        get; set;
    }
    public string? NoticeFileId
    {
        get; set;
    }
    public string? WorkGuidelineFileId
    {
        get; set;
    }
    public bool ShowTodoAuthor { get; set; } = true;
    public bool ShowCalendarAuthor { get; set; } = true;
}
