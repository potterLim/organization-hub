using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private void beginEdit(TodoTaskItem task)
    {
        if (canWriteTask(task) == false)
        {
            return;
        }

        mEditingTaskID = task.ID;
        mEditingTitle = task.Title;
        mDeletingTaskID = string.Empty;
    }

    private void setEditingTitle(string title)
    {
        mEditingTitle = title;
    }

    private void cancelEdit()
    {
        mEditingTaskID = string.Empty;
        mEditingTitle = string.Empty;
    }

    private void requestDelete(TodoTaskItem task)
    {
        if (canWriteTask(task) == false)
        {
            return;
        }

        mDeletingTaskID = task.ID;
        mEditingTaskID = string.Empty;
        mEditingTitle = string.Empty;
    }

    private void cancelDelete()
    {
        mDeletingTaskID = string.Empty;
    }

    private async Task saveEditAsync(TodoTaskItem task)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canWriteTask(task) == false)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            string editedTitle = mEditingTitle.Trim();
            TodoTaskItem optimisticTask = cloneTask(task);
            optimisticTask.Title = editedTitle;
            optimisticTask.UpdatedAt = DateTimeOffset.Now;
            replaceTask(optimisticTask);
            cancelEdit();
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskFormModel formModel = new TodoTaskFormModel();
            formModel.TaskListID = task.TaskListID;
            formModel.TaskID = task.ID;
            formModel.Title = editedTitle;
            TodoTaskItem updatedTask = await TodoService.UpdateTaskAsync(CurrentUserSessionOrNull, formModel, CancellationToken.None);
            updatedTask.TaskListDisplayName = task.TaskListDisplayName;
            replaceTask(updatedTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            mEditingTaskID = task.ID;
            mEditingTitle = task.Title;
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private async Task deleteTaskAsync(TodoTaskItem task)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canWriteTask(task) == false)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            removeTaskFromLocalList(task.TaskListID, task.ID);
            mDeletingTaskID = string.Empty;
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            await TodoService.DeleteTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, CancellationToken.None);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }
}
