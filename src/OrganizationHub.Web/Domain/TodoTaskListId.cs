using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct TodoTaskListId
{
    public const string ALL_TASKS_VALUE = "__organizationhub_all_tasks__";

    private string Value
    {
        get;
    }

    public bool IsAggregate
    {
        get
        {
            return Value == ALL_TASKS_VALUE;
        }
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static TodoTaskListId AllTasks
    {
        get
        {
            return CreateRequired(ALL_TASKS_VALUE);
        }
    }

    public static TodoTaskListId Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static TodoTaskListId CreateOptional(string? taskListIdOrNull)
    {
        return new TodoTaskListId(taskListIdOrNull);
    }

    public static TodoTaskListId CreateRequired(string? taskListIdOrNull)
    {
        TodoTaskListId taskListId = CreateOptional(taskListIdOrNull);
        if (taskListId.HasValue == false)
        {
            throw new ArgumentException("Todo task list ID is required.", nameof(taskListIdOrNull));
        }

        return taskListId;
    }

    private TodoTaskListId(string? taskListIdOrNull)
    {
        Value = string.IsNullOrWhiteSpace(taskListIdOrNull) ? string.Empty : taskListIdOrNull.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Todo task list ID is required.");
        }

        return Value;
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
