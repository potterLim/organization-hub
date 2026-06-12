using System;

namespace OrganizationHub.Web.Data.Entities;

public class TodoTaskMetadata
{
    public int ID
    {
        get; set;
    }
    public string TaskListID { get; set; } = string.Empty;
    public string TaskID { get; set; } = string.Empty;
    public int CreatedByUserID
    {
        get; set;
    }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt
    {
        get; set;
    }
}
