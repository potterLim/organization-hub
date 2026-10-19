using System;
using System.Collections.Generic;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private List<TodoTaskItem> getPendingTasks()
    {
        List<TodoTaskItem> pendingTasks = mTasks.Where(task => task.IsCompleted == false).ToList();
        switch (mTaskSortMode)
        {
            case ETodoTaskSortMode.DueDateAscending:
                return pendingTasks
                    .OrderBy(task => getDueDateSortGroup(task))
                    .ThenBy(task => getTaskDueDateSortValue(task))
                    .ThenBy(task => task.TaskListDisplayName)
                    .ThenBy(task => task.Position)
                    .ThenBy(task => task.Title)
                    .ToList();
            case ETodoTaskSortMode.RecentlyCreated:
                return pendingTasks
                    .OrderByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.TaskListDisplayName)
                    .ThenBy(task => task.Title)
                    .ToList();
            case ETodoTaskSortMode.ManualOrder:
                return pendingTasks;
            default:
                return pendingTasks;
        }
    }

    private List<TodoTaskItem> getCompletedTasks()
    {
        return mTasks.Where(task => task.IsCompleted).OrderByDescending(task => task.CompletedAt).ThenBy(task => task.Title).ToList();
    }

    private List<TodoTaskItem> cloneTasks(List<TodoTaskItem> tasks)
    {
        return tasks.Select(task => cloneTask(task)).ToList();
    }

    private TodoTaskItem cloneTask(TodoTaskItem task)
    {
        TodoTaskItem clonedTask = new TodoTaskItem();
        clonedTask.Id = task.Id;
        clonedTask.TaskListId = task.TaskListId;
        clonedTask.TaskListDisplayName = task.TaskListDisplayName;
        clonedTask.Title = task.Title;
        clonedTask.Position = task.Position;
        clonedTask.CreatedByDisplayName = task.CreatedByDisplayName;
        clonedTask.CreatedByUserIdOrNull = task.CreatedByUserIdOrNull;
        clonedTask.CanEdit = task.CanEdit;
        clonedTask.CanDelete = task.CanDelete;
        clonedTask.IsCompleted = task.IsCompleted;
        clonedTask.CreatedAt = task.CreatedAt;
        clonedTask.UpdatedAt = task.UpdatedAt;
        clonedTask.CompletedAt = task.CompletedAt;
        clonedTask.DueAt = task.DueAt;
        return clonedTask;
    }

    private TodoTaskItem createOptimisticTask(string taskTitle, DateTime? dueAtOrNull)
    {
        TodoTaskItem optimisticTask = new TodoTaskItem();
        optimisticTask.Id = "optimistic-" + Guid.NewGuid().ToString("N");
        optimisticTask.TaskListId = mSelectedTaskListId;
        optimisticTask.CanEdit = mWritableTaskListIds.Contains(mSelectedTaskListId);
        optimisticTask.CanDelete = mDeletableTaskListIds.Contains(mSelectedTaskListId);
        optimisticTask.CreatedByUserIdOrNull = CurrentUserSessionOrNull == null ? null : CurrentUserSessionOrNull.UserAccountId.GetRequiredValue();
        optimisticTask.TaskListDisplayName = getSelectedTaskListDisplayName();
        optimisticTask.Title = taskTitle;
        optimisticTask.Position = string.Empty;
        optimisticTask.CreatedByDisplayName = getCurrentUserDisplayName();
        optimisticTask.IsCompleted = false;
        optimisticTask.CreatedAt = DateTime.UtcNow;
        optimisticTask.UpdatedAt = DateTimeOffset.UtcNow;
        optimisticTask.CompletedAt = null;
        optimisticTask.DueAt = dueAtOrNull;
        return optimisticTask;
    }

    private ETodoDueDateSortGroup getDueDateSortGroup(TodoTaskItem task)
    {
        if (task.DueAt.HasValue == false)
        {
            return ETodoDueDateSortGroup.NoDueDate;
        }

        DateTime dueAtUtc = ClientTimeZoneUtility.normalizeUtcDateTimeValue(task.DueAt.Value);
        if (dueAtUtc < DateTime.UtcNow)
        {
            return ETodoDueDateSortGroup.Overdue;
        }

        return ETodoDueDateSortGroup.Scheduled;
    }

    private DateTime getTaskDueDateSortValue(TodoTaskItem task)
    {
        if (task.DueAt.HasValue == false)
        {
            return DateTime.MaxValue;
        }

        return ClientTimeZoneUtility.normalizeUtcDateTimeValue(task.DueAt.Value);
    }

    private void prependTask(TodoTaskItem task)
    {
        removeTaskFromLocalList(TodoTaskListId.CreateOptional(task.TaskListId), TodoTaskId.CreateOptional(task.Id));
        mTasks.Insert(0, task);
    }

    private void replaceTask(TodoTaskItem task)
    {
        int taskIndex = mTasks.FindIndex(existingTask => existingTask.TaskListId == task.TaskListId && existingTask.Id == task.Id);
        if (taskIndex < 0)
        {
            prependTask(task);
            return;
        }

        mTasks[taskIndex] = task;
    }

    private void removeTaskFromLocalList(TodoTaskListId taskListId, TodoTaskId taskId)
    {
        if (taskListId.HasValue == false || taskId.HasValue == false)
        {
            return;
        }

        string requiredTaskListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        mTasks.RemoveAll(existingTask => existingTask.TaskListId == requiredTaskListId && existingTask.Id == requiredTaskId);
    }

    private void moveTaskInLocalList(TodoTaskItem movedTask, TodoTaskId previousTaskId)
    {
        TodoTaskListId movedTaskListId = TodoTaskListId.CreateOptional(movedTask.TaskListId);
        removeTaskFromLocalList(movedTaskListId, TodoTaskId.CreateOptional(movedTask.Id));
        if (previousTaskId.HasValue == false)
        {
            int firstPendingTaskIndex = mTasks.FindIndex(existingTask => existingTask.IsCompleted == false);
            if (firstPendingTaskIndex < 0)
            {
                mTasks.Insert(0, movedTask);
                return;
            }

            mTasks.Insert(firstPendingTaskIndex, movedTask);
            return;
        }

        string requiredTaskListId = movedTaskListId.GetRequiredValue();
        string requiredPreviousTaskId = previousTaskId.GetRequiredValue();
        int previousTaskIndex = mTasks.FindIndex(existingTask => existingTask.TaskListId == requiredTaskListId && existingTask.Id == requiredPreviousTaskId);
        if (previousTaskIndex < 0)
        {
            mTasks.Insert(0, movedTask);
            return;
        }

        mTasks.Insert(previousTaskIndex + 1, movedTask);
    }

    private enum ETodoDueDateSortGroup
    {
        Overdue,
        Scheduled,
        NoDueDate
    }
}
