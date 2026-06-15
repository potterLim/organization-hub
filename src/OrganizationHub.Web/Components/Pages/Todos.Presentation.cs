using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    private string formatTaskMeta(TodoTaskItem task)
    {
        List<string> metadataParts = new List<string>();
        if (isAggregateSelected() && string.IsNullOrWhiteSpace(task.TaskListDisplayName) == false)
        {
            metadataParts.Add(task.TaskListDisplayName);
        }

        if (mShowTodoAuthor && string.IsNullOrWhiteSpace(task.CreatedByDisplayName) == false)
        {
            metadataParts.Add("작성자: " + task.CreatedByDisplayName);
        }

        if (task.CreatedAt.HasValue)
        {
            metadataParts.Add("작성일: " + formatTaskDateTime(task.CreatedAt.Value));
        }

        if (task.IsCompleted && task.CompletedAt.HasValue)
        {
            metadataParts.Add("완료일: " + formatTaskDateTime(task.CompletedAt.Value));
        }

        return string.Join(" · ", metadataParts);
    }

    private bool isAggregateSelected()
    {
        TodoTaskListId selectedTaskListId = TodoTaskListId.CreateOptional(mSelectedTaskListId);
        TodoTaskListReference? taskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.TaskListId == selectedTaskListId);
        return taskListOrNull != null && taskListOrNull.IsAggregate;
    }

    private string getInitialTaskListId()
    {
        if (mTaskLists.Count == 2 && mTaskLists[0].IsAggregate)
        {
            return mTaskLists[1].TaskListId.GetRequiredValue();
        }

        return mTaskLists[0].TaskListId.GetRequiredValue();
    }

    private bool canCreateTask()
    {
        return string.IsNullOrWhiteSpace(mSelectedTaskListId) == false && isAggregateSelected() == false && isSelectedTaskListWritable();
    }

    private bool canSubmitNewTask()
    {
        return canCreateTask() && string.IsNullOrWhiteSpace(mNewTaskTitle) == false;
    }

    private bool canSortTasks()
    {
        return canCreateTask() && getPendingTasks().Count > 1;
    }

    private bool canDragTask(ETodoTaskCompletionState completionState)
    {
        return completionState != ETodoTaskCompletionState.Completed && canSortTasks();
    }

    private bool isSelectedTaskListWritable()
    {
        return mWritableTaskListIds.Contains(mSelectedTaskListId);
    }

    private bool canWriteTask(TodoTaskItem task)
    {
        return string.IsNullOrWhiteSpace(task.TaskListId) == false && mWritableTaskListIds.Contains(task.TaskListId);
    }

    private string getSelectedTaskListDisplayName()
    {
        TodoTaskListId selectedTaskListId = TodoTaskListId.CreateOptional(mSelectedTaskListId);
        TodoTaskListReference? taskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.TaskListId == selectedTaskListId);
        if (taskListOrNull == null)
        {
            return string.Empty;
        }

        return taskListOrNull.DisplayName;
    }

    private string getCurrentUserDisplayName()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return string.Empty;
        }

        return CurrentUserSessionOrNull.DisplayName;
    }

    private string formatTaskDateTime(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatTaskDateTime(DateTimeOffset dateTime)
    {
        return dateTime.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private void showError(string message)
    {
        mMessage = message;
        mMessageSeverity = Severity.Warning;
    }

    private string createTodoErrorMessage(Exception exception)
    {
        return "할 일 작업을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.";
    }
}
