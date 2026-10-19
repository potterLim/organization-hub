using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        mHasTaskListLoadError = false;
        try
        {
            IReadOnlyList<TodoTaskListReference> taskLists = await TodoService.GetReadableTaskListsAsync(CurrentUserSessionOrNull, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> writableTaskLists = await TodoService.GetWritableTaskListsAsync(CurrentUserSessionOrNull, EResourceOperation.Update, CancellationToken.None);
            mCreatableTaskListIds = (await TodoService.GetWritableTaskListsAsync(CurrentUserSessionOrNull, EResourceOperation.Create, CancellationToken.None)).Select(taskList => taskList.TaskListId.GetRequiredValue()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            mDeletableTaskListIds = (await TodoService.GetWritableTaskListsAsync(CurrentUserSessionOrNull, EResourceOperation.Delete, CancellationToken.None)).Select(taskList => taskList.TaskListId.GetRequiredValue()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            mTaskLists = taskLists.ToList();
            mWritableTaskListIds = writableTaskLists.Select(taskList => taskList.TaskListId.GetRequiredValue()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (mTaskLists.Count > 0)
            {
                mSelectedTaskListId = getInitialTaskListId();
                await loadTasksAsync();
            }
        }
        catch (Exception exception)
        {
            mHasTaskListLoadError = true;
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingLists = false;
    }

    private async Task handleTaskListChangedAsync(string taskListId)
    {
        if (mIsSaving || taskListId == mSelectedTaskListId)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(mEditingTaskId) == false || string.IsNullOrWhiteSpace(mNewTaskTitle) == false || mNewTaskDueAt.HasValue)
        {
            mPendingTaskListIdOrNull = taskListId;
            return;
        }

        await switchTaskListAsync(taskListId);
    }

    private void keepCurrentTaskList()
    {
        mPendingTaskListIdOrNull = null;
    }

    private async Task discardDraftAndSwitchTaskListAsync()
    {
        if (mPendingTaskListIdOrNull == null || mIsSaving)
        {
            return;
        }

        string taskListId = mPendingTaskListIdOrNull;
        mPendingTaskListIdOrNull = null;
        mNewTaskTitle = string.Empty;
        mNewTaskDueAt = null;
        await switchTaskListAsync(taskListId);
    }

    private async Task switchTaskListAsync(string taskListId)
    {
        mSelectedTaskListId = taskListId;
        await ensureAvailableTaskSortModeAsync();
        mEditingTaskId = string.Empty;
        mEditingTitle = string.Empty;
        mEditingDueAt = null;
        mIsEditingDuePickerOpen = false;
        mDeletingTaskId = string.Empty;
        await loadTasksAsync();
    }

    private async Task loadTasksAsync()
    {
        if (CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedTaskListId))
        {
            return;
        }

        mIsLoadingTasks = true;
        mHasTaskLoadError = false;
        mShouldInitializeSortable = false;
        bool shouldInitializeSortable = false;
        try
        {
            IReadOnlyList<ChangeVersionKey> changeVersionKeys = getChangeVersionKeys();
            ChangeVersionReadResult<IReadOnlyList<TodoTaskItem>> readResult = await ChangeVersionService.ReadWithStableSnapshotAsync(
                changeVersionKeys,
                cancellationToken => TodoService.GetTasksAsync(CurrentUserSessionOrNull, TodoTaskListId.CreateOptional(mSelectedTaskListId), cancellationToken),
                CancellationToken.None);
            mTasks = readResult.Value.ToList();
            mMessage = string.Empty;
            mDeletingTaskId = string.Empty;
            EChangeVersionStability stability = readResult.IsStable ? EChangeVersionStability.Stable : EChangeVersionStability.Unstable;
            recordLoadedChangeSnapshot(readResult.Snapshot, stability);
            shouldInitializeSortable = true;
        }
        catch (Exception exception)
        {
            mHasTaskLoadError = true;
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingTasks = false;
        mShouldInitializeSortable = shouldInitializeSortable;
    }
}
