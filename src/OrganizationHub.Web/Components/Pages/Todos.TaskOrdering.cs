using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private async Task moveTaskByKeyAsync(TodoTaskItem task, KeyboardEventArgs args)
    {
        if (args.Key != "ArrowUp" && args.Key != "ArrowDown")
        {
            return;
        }
        List<TodoTaskItem> tasks = getPendingTasks().Where(item => item.TaskListId == task.TaskListId).ToList();
        int index = tasks.FindIndex(item => item.Id == task.Id);
        int target = index + (args.Key == "ArrowUp" ? -1 : 1);
        if (index < 0 || target < 0 || target >= tasks.Count)
        {
            return;
        }
        if (args.Key == "ArrowUp")
        {
            await moveTaskBeforeAsync(TodoTaskId.CreateRequired(task.Id), TodoTaskId.CreateRequired(tasks[target].Id));
        }
        else
        {
            await moveTaskAfterAsync(TodoTaskId.CreateRequired(task.Id), TodoTaskId.CreateRequired(tasks[target].Id));
        }
    }

    [JSInvokable("MoveTaskBeforeFromScript")]
    public async Task MoveTaskBeforeFromScriptAsync(string sourceTaskId, string targetTaskId)
    {
        await moveTaskBeforeAsync(SortableItemMoveRequest.CreateFromScript(sourceTaskId, targetTaskId, ESortableMoveDirection.Before));
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable("MoveTaskAfterFromScript")]
    public async Task MoveTaskAfterFromScriptAsync(string sourceTaskId, string targetTaskId)
    {
        await moveTaskAfterAsync(SortableItemMoveRequest.CreateFromScript(sourceTaskId, targetTaskId, ESortableMoveDirection.After));
        await InvokeAsync(StateHasChanged);
    }

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
        TodoTaskItem targetTask = pendingTasks[targetIndex];
        if (sourceTask.TaskListId != targetTask.TaskListId || canWriteTask(sourceTask) == false)
        {
            return;
        }

        List<TodoTaskItem> sameListPendingTasks = pendingTasks.Where(task => task.TaskListId == sourceTask.TaskListId).ToList();
        sourceIndex = sameListPendingTasks.FindIndex(task => task.Id == requiredSourceTaskId);
        targetIndex = sameListPendingTasks.FindIndex(task => task.Id == requiredTargetTaskId);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        if (targetIndex == sourceIndex || targetIndex == sourceIndex + 1)
        {
            return;
        }

        sameListPendingTasks.RemoveAt(sourceIndex);
        targetIndex = sameListPendingTasks.FindIndex(task => task.Id == requiredTargetTaskId);
        if (targetIndex < 0)
        {
            return;
        }

        TodoTaskId previousTaskId = targetIndex <= 0 ? TodoTaskId.Empty : TodoTaskId.CreateOptional(sameListPendingTasks[targetIndex - 1].Id);
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

        if (sourceTaskOrNull.TaskListId != targetTaskOrNull.TaskListId || canWriteTask(sourceTaskOrNull) == false)
        {
            return;
        }

        List<TodoTaskItem> sameListPendingTasks = pendingTasks.Where(task => task.TaskListId == sourceTaskOrNull.TaskListId).ToList();
        int sourceIndex = sameListPendingTasks.IndexOf(sourceTaskOrNull);
        int targetIndex = sameListPendingTasks.IndexOf(targetTaskOrNull);
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

    private async Task moveTaskBeforeAsync(SortableItemMoveRequest moveRequest)
    {
        if (moveRequest.HasRequiredItems == false)
        {
            return;
        }

        await moveTaskBeforeAsync(
            TodoTaskId.CreateOptional(moveRequest.SourceItemKey.GetOptionalValue()),
            TodoTaskId.CreateOptional(moveRequest.TargetItemKey.GetOptionalValue()));
    }

    private async Task moveTaskAfterAsync(SortableItemMoveRequest moveRequest)
    {
        if (moveRequest.HasRequiredItems == false)
        {
            return;
        }

        await moveTaskAfterAsync(
            TodoTaskId.CreateOptional(moveRequest.SourceItemKey.GetOptionalValue()),
            TodoTaskId.CreateOptional(moveRequest.TargetItemKey.GetOptionalValue()));
    }
}
