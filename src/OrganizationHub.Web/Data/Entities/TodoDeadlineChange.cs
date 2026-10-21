using System;

namespace OrganizationHub.Web.Data.Entities;

public sealed class TodoDeadlineChange
{
    public int Id
    {
        get; set;
    }
    public string TaskListId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string ExpectedETag { get; set; } = string.Empty;
    public string ConfirmedGoogleETag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? DueAt
    {
        get; set;
    }
    public DateTime CreatedAt
    {
        get; set;
    }
}
