using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class TodoTaskListReference
{
    public const string AGGREGATE_TASK_LIST_ID = TodoTaskListId.ALL_TASKS_VALUE;

    public TodoTaskListId TaskListId
    {
        get; private init;
    }
    public string DisplayName { get; private init; } = string.Empty;
    public OrganizationTeamId? TeamIdOrNull
    {
        get; private init;
    }
    public bool IsAggregate
    {
        get; private init;
    }

    public TodoTaskListReference(TodoTaskListId taskListId, string displayName, OrganizationTeamId? teamIdOrNull)
        : this(taskListId, displayName, teamIdOrNull, ETodoTaskListKind.Regular)
    {
    }

    public TodoTaskListReference(TodoTaskListId taskListId, string displayName, OrganizationTeamId? teamIdOrNull, ETodoTaskListKind taskListKind)
    {
        TaskListId = taskListId;
        DisplayName = displayName;
        TeamIdOrNull = teamIdOrNull;
        IsAggregate = taskListKind == ETodoTaskListKind.Aggregate || taskListId.IsAggregate;
    }
}
