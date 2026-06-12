namespace OrganizationHub.Web.Models;

public sealed class TeamFormModel
{
    public int ID
    {
        get; set;
    }
    public string TeamKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder
    {
        get; set;
    }
    public string MaterialsFolderID { get; set; } = string.Empty;
    public string ApprovedFolderID { get; set; } = string.Empty;
    public string CalendarID { get; set; } = string.Empty;
    public string TaskListID { get; set; } = string.Empty;
}
