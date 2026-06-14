using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class TeamFormModel
{
    public int Id
    {
        get; set;
    }
    public OrganizationTeamId TeamIdValue
    {
        get
        {
            return OrganizationTeamId.CreateOptional(Id);
        }
    }
    public string TeamKey { get; set; } = string.Empty;
    public OrganizationTeamKey TeamKeyValue
    {
        get
        {
            return new OrganizationTeamKey(TeamKey);
        }
    }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder
    {
        get; set;
    }
    public string MaterialsFolderId { get; set; } = string.Empty;
    public DriveFolderId MaterialsFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(MaterialsFolderId);
        }
    }
    public string ApprovedFolderId { get; set; } = string.Empty;
    public DriveFolderId ApprovedFolderIdValue
    {
        get
        {
            return DriveFolderId.CreateOptional(ApprovedFolderId);
        }
    }
    public string CalendarId { get; set; } = string.Empty;
    public CalendarId CalendarIdValue
    {
        get
        {
            return OrganizationHub.Web.Domain.CalendarId.CreateOptional(CalendarId);
        }
    }
    public string TaskListId { get; set; } = string.Empty;
    public TodoTaskListId TaskListIdValue
    {
        get
        {
            return TodoTaskListId.CreateOptional(TaskListId);
        }
    }
}
