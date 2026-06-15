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
    private async Task moveTaskBeforeAsync(TodoTaskId sourceTaskId, TodoTaskId targetTaskId)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        if (sourceTaskId.HasValue == false || targetTaskId.HasValue == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        string requiredSourceTaskId = sourceTaskId.GetRequiredValue();
        string requiredTargetTaskId = targetTaskId.GetRequiredValue();
        int sourceIndex = pendingTasks.FindIndex(task => task.Id == requiredSourceTaskId);
        int targetIndex = pendingTasks.FindIndex(task => task.Id == requiredTargetTaskId);
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
        targetIndex = pendingTasks.FindIndex(task => task.Id == requiredTargetTaskId);
        if (targetIndex < 0)
        {
            return;
        }

        TodoTaskId previousTaskId = targetIndex <= 0 ? TodoTaskId.Empty : TodoTaskId.CreateOptional(pendingTasks[targetIndex - 1].Id);
        await moveTaskAsync(sourceTask, previousTaskId);
    }

    private async Task moveTaskAfterAsync(TodoTaskId sourceTaskId, TodoTaskId targetTaskId)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        if (sourceTaskId.HasValue == false || targetTaskId.HasValue == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        string requiredSourceTaskId = sourceTaskId.GetRequiredValue();
        string requiredTargetTaskId = targetTaskId.GetRequiredValue();
        TodoTaskItem? sourceTaskOrNull = pendingTasks.FirstOrDefault(task => task.Id == requiredSourceTaskId);
        TodoTaskItem? targetTaskOrNull = pendingTasks.FirstOrDefault(task => task.Id == requiredTargetTaskId);
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

        await moveTaskAsync(sourceTaskOrNull, TodoTaskId.CreateOptional(targetTaskOrNull.Id));
    }

    private async Task moveTaskAsync(TodoTaskItem task, TodoTaskId previousTaskId)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            moveTaskInLocalList(task, previousTaskId);
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskItem movedTask = await TodoService.MoveTaskAsync(CurrentUserSessionOrNull, TodoTaskListId.CreateOptional(task.TaskListId), TodoTaskId.CreateOptional(task.Id), previousTaskId, CancellationToken.None);
            movedTask.TaskListDisplayName = task.TaskListDisplayName;
            moveTaskInLocalList(movedTask, previousTaskId);
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
    public async Task MoveTaskBeforeFromScriptAsync(string sourceTaskId, string targetTaskId)
    {
        await moveTaskBeforeAsync(TodoTaskId.CreateOptional(sourceTaskId), TodoTaskId.CreateOptional(targetTaskId));
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable("MoveTaskAfterFromScript")]
    public async Task MoveTaskAfterFromScriptAsync(string sourceTaskId, string targetTaskId)
    {
        await moveTaskAfterAsync(TodoTaskId.CreateOptional(sourceTaskId), TodoTaskId.CreateOptional(targetTaskId));
        await InvokeAsync(StateHasChanged);
    }
}
