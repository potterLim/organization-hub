using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private async Task setTaskCompletionStateAsync(TodoTaskItem task, ETodoTaskCompletionState completionState)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canWriteTask(task) == false)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            TodoTaskItem optimisticTask = cloneTask(task);
            bool isCompleted = completionState == ETodoTaskCompletionState.Completed;
            optimisticTask.IsCompleted = isCompleted;
            optimisticTask.CompletedAt = isCompleted ? DateTimeOffset.Now : null;
            optimisticTask.UpdatedAt = DateTimeOffset.Now;
            replaceTask(optimisticTask);
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskItem updatedTask = await saveTaskCompletionStateAsync(CurrentUserSessionOrNull, task, completionState);
            updatedTask.TaskListDisplayName = task.TaskListDisplayName;
            replaceTask(updatedTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private async Task<TodoTaskItem> saveTaskCompletionStateAsync(UserSession userSession, TodoTaskItem task, ETodoTaskCompletionState completionState)
    {
        if (completionState == ETodoTaskCompletionState.Completed)
        {
            return await TodoService.CompleteTaskAsync(userSession, task.TaskListID, task.ID, CancellationToken.None);
        }

        return await TodoService.ReopenTaskAsync(userSession, task.TaskListID, task.ID, CancellationToken.None);
    }
}
