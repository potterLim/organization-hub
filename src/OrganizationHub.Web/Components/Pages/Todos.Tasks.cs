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
    private async Task loadTaskListsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        mIsLoadingLists = true;
        try
        {
            IReadOnlyList<TodoTaskListReference> taskLists = await TodoService.GetReadableTaskListsAsync(CurrentUserSessionOrNull, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> writableTaskLists = await TodoService.GetWritableTaskListsAsync(CurrentUserSessionOrNull, CancellationToken.None);
            mTaskLists = taskLists.ToList();
            mWritableTaskListIDs = writableTaskLists.Select(taskList => taskList.TaskListID).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (mTaskLists.Count > 0)
            {
                mSelectedTaskListID = getInitialTaskListID();
                await loadTasksAsync();
            }
        }
        catch (Exception exception)
        {
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingLists = false;
    }

    private async Task handleTaskListChangedAsync(string taskListID)
    {
        mSelectedTaskListID = taskListID;
        mEditingTaskID = string.Empty;
        mEditingTitle = string.Empty;
        mDeletingTaskID = string.Empty;
        await loadTasksAsync();
    }

    private async Task loadTasksAsync()
    {
        if (CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedTaskListID))
        {
            return;
        }

        mIsLoadingTasks = true;
        mShouldInitializeSortable = false;
        bool shouldInitializeSortable = false;
        try
        {
            IReadOnlyList<string> changeVersionKeys = getChangeVersionKeys();
            ChangeVersionReadResult<IReadOnlyList<TodoTaskItem>> readResult = await ChangeVersionService.ReadWithStableSnapshotAsync(
                changeVersionKeys,
                cancellationToken => TodoService.GetTasksAsync(CurrentUserSessionOrNull, mSelectedTaskListID, cancellationToken),
                CancellationToken.None);
            mTasks = readResult.Value.ToList();
            mMessage = string.Empty;
            mDeletingTaskID = string.Empty;
            EChangeVersionStability stability = readResult.IsStable ? EChangeVersionStability.Stable : EChangeVersionStability.Unstable;
            recordLoadedChangeSnapshot(readResult.Snapshot, stability);
            shouldInitializeSortable = true;
        }
        catch (Exception exception)
        {
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingTasks = false;
        mShouldInitializeSortable = shouldInitializeSortable;
    }

    private async Task handleNewTaskKeyDownAsync(KeyboardEventArgs keyboardEventArgs)
    {
        if (keyboardEventArgs.Key == "Enter")
        {
            await createTaskAsync();
        }
    }

    private async Task createTaskAsync()
    {
        if (CurrentUserSessionOrNull == null || mIsSaving || canSubmitNewTask() == false)
        {
            return;
        }

        mIsSaving = true;
        string taskTitle = mNewTaskTitle.Trim();
        TodoTaskItem optimisticTask = createOptimisticTask(taskTitle);
        try
        {
            prependTask(optimisticTask);
            mNewTaskTitle = string.Empty;
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskFormModel formModel = new TodoTaskFormModel();
            formModel.TaskListID = mSelectedTaskListID;
            formModel.Title = taskTitle;
            TodoTaskItem createdTask = await TodoService.CreateTaskAsync(CurrentUserSessionOrNull, formModel, CancellationToken.None);
            removeTaskFromLocalList(optimisticTask.TaskListID, optimisticTask.ID);
            prependTask(createdTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            removeTaskFromLocalList(optimisticTask.TaskListID, optimisticTask.ID);
            if (string.IsNullOrWhiteSpace(mNewTaskTitle))
            {
                mNewTaskTitle = taskTitle;
            }

            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

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
            TodoTaskItem updatedTask = completionState == ETodoTaskCompletionState.Completed ? await TodoService.CompleteTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, CancellationToken.None) : await TodoService.ReopenTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, CancellationToken.None);
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

    private async Task moveTaskBeforeAsync(string sourceTaskID, string targetTaskID)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        int sourceIndex = pendingTasks.FindIndex(task => task.ID == sourceTaskID);
        int targetIndex = pendingTasks.FindIndex(task => task.ID == targetTaskID);
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
        targetIndex = pendingTasks.FindIndex(task => task.ID == targetTaskID);
        if (targetIndex < 0)
        {
            return;
        }

        string previousTaskID = targetIndex <= 0 ? string.Empty : pendingTasks[targetIndex - 1].ID;
        await moveTaskAsync(sourceTask, previousTaskID);
    }

    private async Task moveTaskAfterAsync(string sourceTaskID, string targetTaskID)
    {
        if (canSortTasks() == false)
        {
            return;
        }

        List<TodoTaskItem> pendingTasks = getPendingTasks();
        TodoTaskItem? sourceTaskOrNull = pendingTasks.FirstOrDefault(task => task.ID == sourceTaskID);
        TodoTaskItem? targetTaskOrNull = pendingTasks.FirstOrDefault(task => task.ID == targetTaskID);
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

        await moveTaskAsync(sourceTaskOrNull, targetTaskOrNull.ID);
    }

    private async Task moveTaskAsync(TodoTaskItem task, string previousTaskID)
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
            TodoTaskItem movedTask = await TodoService.MoveTaskAsync(CurrentUserSessionOrNull, task.TaskListID, task.ID, previousTaskID, CancellationToken.None);
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
        await moveTaskBeforeAsync(sourceTaskID, targetTaskID);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable("MoveTaskAfterFromScript")]
    public async Task MoveTaskAfterFromScriptAsync(string sourceTaskID, string targetTaskID)
    {
        await moveTaskAfterAsync(sourceTaskID, targetTaskID);
        await InvokeAsync(StateHasChanged);
    }

}
