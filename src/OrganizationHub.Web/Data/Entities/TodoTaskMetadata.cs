using System;

namespace OrganizationHub.Web.Data.Entities;

public class TodoTaskMetadata
{
    public int Id
    {
        get; set;
    }
    public string TaskListId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public int CreatedByUserId
    {
        get; set;
    }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt
    {
        get; set;
    }
    public DateTime? DueAt
    {
        get; set;
    }
}
