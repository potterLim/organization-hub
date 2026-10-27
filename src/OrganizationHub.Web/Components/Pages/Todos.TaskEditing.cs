using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private bool isEditingTask(TodoTaskItem task)
    {
        return mEditingTaskId == task.Id && mEditingTaskListId == task.TaskListId;
    }

    private bool isDeletingTask(TodoTaskItem task)
    {
        return mDeletingTaskId == task.Id && mDeletingTaskListId == task.TaskListId;
    }

    private void beginEdit(TodoTaskItem task)
    {
        if (canWriteTask(task) == false)
        {
            return;
        }

        mEditingTaskId = task.Id;
        mEditingTaskListId = task.TaskListId;
        mEditingTitle = task.Title;
        mEditingETag = task.ETag;
        mHasEditConflict = false;
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
        mPendingTaskListIdOrNull = null;
        mEditingTaskId = string.Empty;
        mHasEditConflict = false;
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
        mDeletingTaskListId = task.TaskListId;
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
        string editedTitle = mEditingTitle.Trim();
        DateTime? editedDueAtUtcOrNull = createDueAtUtcOrNull(mEditingDueAt);
        try
        {
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
        catch (ResourceSynchronizationException exception)
        {
            await loadTasksAsync();
            showError(exception.Message);
            if (exception.IsWriteConfirmed)
            {
                mMessageSeverity = MudBlazor.Severity.Info;
            }
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            mEditingTaskId = task.Id;
            mEditingTaskListId = task.TaskListId;
            mEditingTitle = editedTitle;
            mEditingDueAt = createEditorDueAtOrNull(editedDueAtUtcOrNull);
            mHasEditConflict = exception is ResourceEditConflictException;
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private async Task reloadConflictedTaskAsync()
    {
        mPreviousDraft = mEditingTitle + " · 마감: " + (mEditingDueAt.HasValue ? mEditingDueAt.Value.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "없음");
        string taskId = mEditingTaskId;
        string taskListId = mEditingTaskListId;
        cancelEdit();
        await loadTasksAsync();
        TodoTaskItem? latestOrNull = mTasks.FirstOrDefault(item => item.Id == taskId && item.TaskListId == taskListId);
        if (latestOrNull != null)
        {
            beginEdit(latestOrNull);
        }
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
            await TodoService.DeleteTaskAsync(CurrentUserSessionOrNull, TodoTaskListId.CreateOptional(task.TaskListId), TodoTaskId.CreateOptional(task.Id), task.ETag, CancellationToken.None);
            recordLoadedChangeSnapshot();
        }
        catch (ResourceEditConflictException)
        {
            await loadTasksAsync();
            showError("다른 사용자가 할 일을 변경하여 삭제하지 않았습니다. 최신 내용을 확인한 뒤 다시 삭제해 주세요.");
        }
        catch (ResourceDeletionUncertainException exception)
        {
            await loadTasksAsync();
            showError(exception.Message);
        }
        catch (Exception exception)
        {
            mTasks = cloneTasks(previousTasks);
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }
}
