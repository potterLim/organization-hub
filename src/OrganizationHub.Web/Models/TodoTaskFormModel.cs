namespace OrganizationHub.Web.Models;

public sealed class TodoTaskFormModel
{
    public string TaskListID { get; set; } = string.Empty;
    public string TaskID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
