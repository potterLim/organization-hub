using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Tests.Services;

public class TodoReadProxy : DispatchProxy
{
    public bool ShouldFail { get; set; }

    protected override object? Invoke(MethodInfo? targetMethodOrNull, object?[]? argumentsOrNull)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("Simulated Google read failure");
        }

        if (targetMethodOrNull!.Name == nameof(ITodoService.GetTasksAsync))
        {
            return Task.FromResult<IReadOnlyList<TodoTaskItem>>(new List<TodoTaskItem>());
        }

        return Task.FromResult<IReadOnlyList<TodoTaskListReference>>(new List<TodoTaskListReference>());
    }
}
