using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class TodoTaskListReference
{
    public const string AGGREGATE_TASK_LIST_ID = "__organizationhub_all_tasks__";

    public string TaskListID { get; private init; } = string.Empty;
    public string DisplayName { get; private init; } = string.Empty;
    public int? TeamID
    {
        get; private init;
    }
    public bool IsAggregate
    {
        get; private init;
    }

    public TodoTaskListReference(string taskListID, string displayName, int? teamID)
        : this(taskListID, displayName, teamID, ETodoTaskListKind.Regular)
    {
    }

    public TodoTaskListReference(string taskListID, string displayName, int? teamID, ETodoTaskListKind taskListKind)
    {
        TaskListID = taskListID;
        DisplayName = displayName;
        TeamID = teamID;
        IsAggregate = taskListKind == ETodoTaskListKind.Aggregate;
    }

    public static bool IsAggregateTaskListID(string taskListID)
    {
        return taskListID == AGGREGATE_TASK_LIST_ID;
    }
}
