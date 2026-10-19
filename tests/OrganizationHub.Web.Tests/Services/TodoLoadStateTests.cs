using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TodoLoadStateTests
{
    [Fact]
    public async Task FailedTaskReadKeepsPreviousDataAndRetryClearsErrorAsync()
    {
        Todos page = createPage();
        List<TodoTaskItem> previousTasks = new List<TodoTaskItem>();
        TodoTaskItem previousTask = new TodoTaskItem();
        previousTask.Id = "existing";
        previousTasks.Add(previousTask);
        setField(page, "mTasks", previousTasks);
        TodoReadProxy proxy = (TodoReadProxy)getService(page);
        proxy.ShouldFail = true;
        await invokeAsync(page, "loadTasksAsync");
        Assert.True(getFlag(page, "mHasTaskLoadError"));
        Assert.Same(previousTasks, getField(page, "mTasks"));
        Assert.False(getFlag(page, "mIsLoadingTasks"));
        proxy.ShouldFail = false;
        await invokeAsync(page, "loadTasksAsync");
        Assert.False(getFlag(page, "mHasTaskLoadError"));
        Assert.Empty((List<TodoTaskItem>)getField(page, "mTasks"));
    }

    [Fact]
    public async Task FailedListReadIsDistinctFromNoConnectedListsAsync()
    {
        Todos page = createPage();
        ((TodoReadProxy)getService(page)).ShouldFail = true;
        await invokeAsync(page, "loadTaskListsAsync");
        Assert.True(getFlag(page, "mHasTaskListLoadError"));
        Assert.False(getFlag(page, "mIsLoadingLists"));
    }

    private static Todos createPage()
    {
        Todos page = new Todos();
        typeof(OrganizationHubComponentBase).GetProperty("CurrentUserSessionOrNull", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, OperationPermissionRegressionTests.createSession());
        typeof(Todos).GetProperty("TodoService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, DispatchProxy.Create<ITodoService, TodoReadProxy>());
        typeof(Todos).GetProperty("ChangeVersionService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, new ChangeVersionService());
        setField(page, "mSelectedTaskListId", "tasks");
        return page;
    }

    private static ITodoService getService(Todos page)
    {
        return (ITodoService)typeof(Todos).GetProperty("TodoService", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
    }

    private static Task invokeAsync(Todos page, string methodName)
    {
        return (Task)typeof(Todos).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
    }

    private static void setField(Todos page, string name, object fieldValue)
    {
        typeof(Todos).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, fieldValue);
    }

    private static object getField(Todos page, string name)
    {
        return typeof(Todos).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
    }

    private static bool getFlag(Todos page, string name)
    {
        return (bool)getField(page, name);
    }
}
