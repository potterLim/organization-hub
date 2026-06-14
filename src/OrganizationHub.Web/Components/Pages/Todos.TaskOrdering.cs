using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private async Task moveTaskBeforeAsync(TodoTaskID sourceTaskID, TodoTaskID targetTaskID)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        int sourceIndex = pendingTasks.FindIndex(task => task.ID == sourceTaskID.Value);
        int targetIndex = pendingTasks.FindIndex(task => task.ID == targetTaskID.Value);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        TodoTaskItem sourceTask = pendingTasks[sourceIndex];
        if (targetIndex == sourceIndex || targetIndex == sourceIndex + 1)
        {
            return;
        }

        pendingTasks.RemoveAt(sourceIndex);
        targetIndex = pendingTasks.FindIndex(task => task.ID == targetTaskID.Value);
        if (targetIndex < 0)
        {
            return;
        }

        TodoTaskID previousTaskID = targetIndex <= 0 ? TodoTaskID.Empty : new TodoTaskID(pendingTasks[targetIndex - 1].ID);
        await moveTaskAsync(sourceTask, previousTaskID);
    }

    private async Task moveTaskAfterAsync(TodoTaskID sourceTaskID, TodoTaskID targetTaskID)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        TodoTaskItem? sourceTaskOrNull = pendingTasks.FirstOrDefault(task => task.ID == sourceTaskID.Value);
        TodoTaskItem? targetTaskOrNull = pendingTasks.FirstOrDefault(task => task.ID == targetTaskID.Value);
        if (sourceTaskOrNull == null || targetTaskOrNull == null)
        {
            return;
        }

        int sourceIndex = pendingTasks.IndexOf(sourceTaskOrNull);
        int targetIndex = pendingTasks.IndexOf(targetTaskOrNull);
        if (targetIndex == sourceIndex || targetIndex == sourceIndex - 1)
        {
            return;
        }

        await moveTaskAsync(sourceTaskOrNull, new TodoTaskID(targetTaskOrNull.ID));
    }

    private async Task moveTaskAsync(TodoTaskItem task, TodoTaskID previousTaskID)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            moveTaskInLocalList(task, previousTaskID);
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskItem movedTask = await TodoService.MoveTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, previousTaskID.Value, CancellationToken.None);
            movedTask.TaskListDisplayName = task.TaskListDisplayName;
            moveTaskInLocalList(movedTask, previousTaskID);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    [JSInvokable("MoveTaskBeforeFromScript")]
    public async Task MoveTaskBeforeFromScriptAsync(string sourceTaskID, string targetTaskID)
    {
        await moveTaskBeforeAsync(new TodoTaskID(sourceTaskID), new TodoTaskID(targetTaskID));
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable("MoveTaskAfterFromScript")]
    public async Task MoveTaskAfterFromScriptAsync(string sourceTaskID, string targetTaskID)
    {
        await moveTaskAfterAsync(new TodoTaskID(sourceTaskID), new TodoTaskID(targetTaskID));
        await InvokeAsync(StateHasChanged);
    }
}
