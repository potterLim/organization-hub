using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class TodoTaskFormModel
{
    public string TaskListId { get; set; } = string.Empty;
    public TodoTaskListId TaskListIdValue
    {
        get
        {
            return TodoTaskListId.CreateOptional(TaskListId);
        }
    }
    public string TaskId { get; set; } = string.Empty;
    public TodoTaskId TaskIdValue
    {
        get
        {
            return TodoTaskId.CreateOptional(TaskId);
        }
    }
    public string Title { get; set; } = string.Empty;
}
