using System;

namespace OrganizationHub.Web.Models;

public sealed class TodoTaskItem
{
    public string ID { get; set; } = string.Empty;
    public string TaskListID { get; set; } = string.Empty;
    public string TaskListDisplayName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public bool IsCompleted
    {
        get; set;
    }
    public DateTime? CreatedAt
    {
        get; set;
    }
    public DateTimeOffset? UpdatedAt
    {
        get; set;
    }
    public DateTimeOffset? CompletedAt
    {
        get; set;
    }
}
