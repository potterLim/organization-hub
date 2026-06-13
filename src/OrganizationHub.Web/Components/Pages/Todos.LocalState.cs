using System;
using System.Collections.Generic;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private List<TodoTaskItem> getPendingTasks()
    {
        return mTasks.Where(task => task.IsCompleted == false).ToList();
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
        clonedTask.ID = task.ID;
        clonedTask.TaskListID = task.TaskListID;
        clonedTask.TaskListDisplayName = task.TaskListDisplayName;
        clonedTask.Title = task.Title;
        clonedTask.Position = task.Position;
        clonedTask.CreatedByDisplayName = task.CreatedByDisplayName;
        clonedTask.IsCompleted = task.IsCompleted;
        clonedTask.CreatedAt = task.CreatedAt;
        clonedTask.UpdatedAt = task.UpdatedAt;
        clonedTask.CompletedAt = task.CompletedAt;
        return clonedTask;
    }

    private TodoTaskItem createOptimisticTask(string taskTitle)
    {
        TodoTaskItem optimisticTask = new TodoTaskItem();
        optimisticTask.ID = "optimistic-" + Guid.NewGuid().ToString("N");
        optimisticTask.TaskListID = mSelectedTaskListID;
        optimisticTask.TaskListDisplayName = getSelectedTaskListDisplayName();
        optimisticTask.Title = taskTitle;
        optimisticTask.Position = string.Empty;
        optimisticTask.CreatedByDisplayName = getCurrentUserDisplayName();
        optimisticTask.IsCompleted = false;
        optimisticTask.CreatedAt = DateTime.Now;
        optimisticTask.UpdatedAt = DateTimeOffset.Now;
        optimisticTask.CompletedAt = null;
        return optimisticTask;
    }

    private void prependTask(TodoTaskItem task)
    {
        removeTaskFromLocalList(task.TaskListID, task.ID);
        mTasks.Insert(0, task);
    }

    private void replaceTask(TodoTaskItem task)
    {
        int taskIndex = mTasks.FindIndex(existingTask => existingTask.TaskListID == task.TaskListID && existingTask.ID == task.ID);
        if (taskIndex < 0)
        {
            prependTask(task);
            return;
        }

        mTasks[taskIndex] = task;
    }

    private void removeTaskFromLocalList(string taskListID, string taskID)
    {
        mTasks.RemoveAll(existingTask => existingTask.TaskListID == taskListID && existingTask.ID == taskID);
    }

    private void moveTaskInLocalList(TodoTaskItem movedTask, string previousTaskID)
    {
        removeTaskFromLocalList(movedTask.TaskListID, movedTask.ID);
        if (string.IsNullOrWhiteSpace(previousTaskID))
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

        int previousTaskIndex = mTasks.FindIndex(existingTask => existingTask.TaskListID == movedTask.TaskListID && existingTask.ID == previousTaskID);
        if (previousTaskIndex < 0)
        {
            mTasks.Insert(0, movedTask);
            return;
        }

        mTasks.Insert(previousTaskIndex + 1, movedTask);
    }
}
