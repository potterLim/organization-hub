using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
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

        mEditingTaskId = task.Id;
        mEditingTitle = task.Title;
        mDeletingTaskId = string.Empty;
    }

    private void setEditingTitle(string title)
    {
        mEditingTitle = title;
    }

    private void cancelEdit()
    {
        mEditingTaskId = string.Empty;
        mEditingTitle = string.Empty;
    }

    private void requestDelete(TodoTaskItem task)
    {
        if (canWriteTask(task) == false)
        {
            return;
        }

        mDeletingTaskId = task.Id;
        mEditingTaskId = string.Empty;
        mEditingTitle = string.Empty;
    }

    private void cancelDelete()
    {
        mDeletingTaskId = string.Empty;
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
            formModel.TaskListId = task.TaskListId;
            formModel.TaskId = task.Id;
            formModel.Title = editedTitle;
            TodoTaskItem updatedTask = await TodoService.UpdateTaskAsync(CurrentUserSessionOrNull, formModel, CancellationToken.None);
            updatedTask.TaskListDisplayName = task.TaskListDisplayName;
            replaceTask(updatedTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            mEditingTaskId = task.Id;
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
            removeTaskFromLocalList(task.TaskListId, task.Id);
            mDeletingTaskId = string.Empty;
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            await TodoService.DeleteTaskAsync(CurrentUserSessionOrNull, TodoTaskListId.CreateOptional(task.TaskListId), TodoTaskId.CreateOptional(task.Id), CancellationToken.None);
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
