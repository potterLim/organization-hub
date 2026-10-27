using System.Reflection;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TodoIdentityTests
{
    [Theory]
    [InlineData("isEditingTask", "mEditing")]
    [InlineData("isDeletingTask", "mDeleting")]
    public void TaskStateBelongsToItsList(string method, string prefix)
    {
        const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;
        Todos page = new Todos();
        typeof(Todos).GetField(prefix + "TaskId", FLAGS)!.SetValue(page, "same-id");
        FieldInfo? listFieldOrNull = typeof(Todos).GetField(prefix + "TaskListId", FLAGS);
        Assert.NotNull(listFieldOrNull);
        listFieldOrNull.SetValue(page, "first-list");
        MethodInfo? stateMethodOrNull = typeof(Todos).GetMethod(method, FLAGS);
        Assert.NotNull(stateMethodOrNull);
        TodoTaskItem first = new TodoTaskItem { Id = "same-id", TaskListId = "first-list" };
        TodoTaskItem second = new TodoTaskItem { Id = "same-id", TaskListId = "second-list" };
        Assert.True((bool)stateMethodOrNull.Invoke(page, new object[] { first })!);
        Assert.False((bool)stateMethodOrNull.Invoke(page, new object[] { second })!);
    }
}
