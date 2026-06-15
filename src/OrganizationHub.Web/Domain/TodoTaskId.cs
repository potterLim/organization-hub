using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct TodoTaskId
{
    private string Value
    {
        get;
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static TodoTaskId Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static TodoTaskId CreateOptional(string? taskIdOrNull)
    {
        return new TodoTaskId(taskIdOrNull);
    }

    public static TodoTaskId CreateRequired(string? taskIdOrNull)
    {
        TodoTaskId taskId = CreateOptional(taskIdOrNull);
        if (taskId.HasValue == false)
        {
            throw new ArgumentException("Todo task ID is required.", nameof(taskIdOrNull));
        }

        return taskId;
    }

    private TodoTaskId(string? taskIdOrNull)
    {
        Value = string.IsNullOrWhiteSpace(taskIdOrNull) ? string.Empty : taskIdOrNull.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Todo task ID is required.");
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
