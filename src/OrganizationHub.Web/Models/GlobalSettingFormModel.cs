namespace OrganizationHub.Web.Models;

public sealed class GlobalSettingFormModel
{
    public int Id
    {
        get; set;
    }
    public GlobalSettingDriveResourceFormModel DriveResources { get; } = new GlobalSettingDriveResourceFormModel();
    public GlobalSettingCalendarResourceFormModel CalendarResources { get; } = new GlobalSettingCalendarResourceFormModel();
    public GlobalSettingTaskResourceFormModel TaskResources { get; } = new GlobalSettingTaskResourceFormModel();
    public GlobalSettingFileResourceFormModel FileResources { get; } = new GlobalSettingFileResourceFormModel();
    public bool ShowTodoAuthor { get; set; } = true;
    public bool ShowCalendarAuthor { get; set; } = true;
}
