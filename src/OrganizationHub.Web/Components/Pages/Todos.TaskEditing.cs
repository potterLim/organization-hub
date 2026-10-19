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
        mEditingETag = task.ETag;
        mEditingDueAt = createEditorDueAtOrNull(task.DueAt);
        mIsEditingDuePickerOpen = false;
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
        mEditingDueAt = null;
        mIsEditingDuePickerOpen = false;
    }

    private void requestDelete(TodoTaskItem task)
    {
        if (canDeleteTask(task) == false)
        {
            return;
        }

        mDeletingTaskId = task.Id;
        mEditingTaskId = string.Empty;
        mEditingTitle = string.Empty;
        mEditingDueAt = null;
        mIsEditingDuePickerOpen = false;
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
            DateTime? editedDueAtUtcOrNull = createDueAtUtcOrNull(mEditingDueAt);
            TodoTaskItem optimisticTask = cloneTask(task);
            optimisticTask.Title = editedTitle;
            optimisticTask.DueAt = editedDueAtUtcOrNull;
            optimisticTask.UpdatedAt = DateTimeOffset.UtcNow;
            replaceTask(optimisticTask);
            cancelEdit();
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskFormModel formModel = new TodoTaskFormModel();
            formModel.TaskListId = task.TaskListId;
            formModel.TaskId = task.Id;
            formModel.ETag = mEditingETag;
            formModel.Title = editedTitle;
            formModel.DueAtUtc = editedDueAtUtcOrNull;
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
            mEditingDueAt = createEditorDueAtOrNull(task.DueAt);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private Task setEditingDueAtAsync(DateTime? dueAtOrNull)
    {
        mEditingDueAt = dueAtOrNull;
        return Task.CompletedTask;
    }

    private Task setEditingDuePickerOpenAsync(bool isOpen)
    {
        mIsEditingDuePickerOpen = isOpen;
        return Task.CompletedTask;
    }

    private async Task deleteTaskAsync(TodoTaskItem task)
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canDeleteTask(task) == false)
        {
            return;
        }

        mIsSaving = true;
        List<TodoTaskItem> previousTasks = cloneTasks(mTasks);
        try
        {
            removeTaskFromLocalList(TodoTaskListId.CreateOptional(task.TaskListId), TodoTaskId.CreateOptional(task.Id));
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
