using System;

namespace OrganizationHub.Web.Models;

public sealed class TodoTaskItem
{
    public string Id { get; set; } = string.Empty;
    public string TaskListId { get; set; } = string.Empty;
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
