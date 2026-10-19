namespace OrganizationHub.Web.Models;

public sealed class GlobalSettingFormModel
{
    public int DisplayOptionsRevision
    {
        get; set;
    }
    public int FileResourcesRevision
    {
        get; set;
    }
    public int Id
    {
        get; set;
    }
    public GlobalSettingDriveResourceFormModel DriveResources { get; } = new GlobalSettingDriveResourceFormModel();
    public GlobalSettingCalendarResourceFormModel CalendarResources { get; } = new GlobalSettingCalendarResourceFormModel();
    public GlobalSettingTaskResourceFormModel TaskResources { get; } = new GlobalSettingTaskResourceFormModel();
    public GlobalSettingFileResourceFormModel FileResources { get; } = new GlobalSettingFileResourceFormModel();
    public bool ShouldShowTodoAuthor { get; set; } = true;
    public bool ShouldShowCalendarAuthor { get; set; } = true;
}
