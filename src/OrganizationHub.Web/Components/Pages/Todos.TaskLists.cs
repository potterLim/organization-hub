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
        try
        {
            IReadOnlyList<TodoTaskListReference> taskLists = await TodoService.GetReadableTaskListsAsync(CurrentUserSessionOrNull, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> writableTaskLists = await TodoService.GetWritableTaskListsAsync(CurrentUserSessionOrNull, CancellationToken.None);
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
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingLists = false;
    }

    private async Task handleTaskListChangedAsync(string taskListId)
    {
        mSelectedTaskListId = taskListId;
        mEditingTaskId = string.Empty;
        mEditingTitle = string.Empty;
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
            showError(createTodoErrorMessage(exception));
        }

        mIsLoadingTasks = false;
        mShouldInitializeSortable = shouldInitializeSortable;
    }
}
