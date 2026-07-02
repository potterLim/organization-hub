using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

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

        if (mShouldShowTodoAuthor && string.IsNullOrWhiteSpace(task.CreatedByDisplayName) == false)
        {
            metadataParts.Add("작성자: " + task.CreatedByDisplayName);
        }

        return string.Join(" · ", metadataParts);
    }

    private string formatTaskTrailingMeta(TodoTaskItem task)
    {
        if (task.IsCompleted && task.CompletedAt.HasValue)
        {
            return "완료일: " + formatTaskDateTime(task.CompletedAt.Value);
        }

        if (task.IsCompleted == false && task.DueAt.HasValue)
        {
            if (isTaskOverdue(task))
            {
                return "기한 지남: " + formatTaskDateTime(task.DueAt.Value);
            }

            return "마감: " + formatTaskDateTime(task.DueAt.Value);
        }

        return string.Empty;
    }

    private string getTaskTrailingMetaClass(TodoTaskItem task)
    {
        if (task.IsCompleted == false && isTaskOverdue(task))
        {
            return "hub-due-status hub-due-status-overdue";
        }

        if (task.IsCompleted == false && isTaskDueToday(task))
        {
            return "hub-due-status hub-due-status-today";
        }

        return string.Empty;
    }

    private DateTime getClientToday()
    {
        return ClientTimeZoneUtility.getClientNow(mClientTimeZoneInfo).Date;
    }

    private bool isAggregateSelected()
    {
        TodoTaskListId selectedTaskListId = TodoTaskListId.CreateOptional(mSelectedTaskListId);
        TodoTaskListReference? taskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.TaskListId == selectedTaskListId);
        return taskListOrNull != null && taskListOrNull.IsAggregate;
    }

    private string getInitialTaskListId()
    {
        List<TodoTaskListReference> regularTaskLists = mTaskLists.Where(taskList => taskList.IsAggregate == false).ToList();
        if (regularTaskLists.Count >= 2)
        {
            TodoTaskListReference? aggregateTaskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.IsAggregate);
            if (aggregateTaskListOrNull != null)
            {
                return aggregateTaskListOrNull.TaskListId.GetRequiredValue();
            }
        }

        if (regularTaskLists.Count > 0)
        {
            return regularTaskLists[0].TaskListId.GetRequiredValue();
        }

        return mTaskLists[0].TaskListId.GetRequiredValue();
    }

    private bool canShowTaskCreation()
    {
        return mWritableTaskListIds.Count > 0;
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
        return canUseManualTaskSort() && mTaskSortMode == ETodoTaskSortMode.ManualOrder && getPendingTasks().Count > 1;
    }

    private string getNewTaskPlaceholder()
    {
        if (canCreateTask())
        {
            return "새 할 일";
        }

        if (isAggregateSelected())
        {
            return "목록을 선택해야 추가할 수 있습니다";
        }

        return "이 목록에는 추가 권한이 없습니다";
    }

    private bool canDragTask(TodoTaskItem task, ETodoTaskCompletionState completionState)
    {
        return completionState != ETodoTaskCompletionState.Completed && canSortTasks() && canWriteTask(task) && hasSortableTaskInSameList(task);
    }

    private bool hasSortableTaskInSameList(TodoTaskItem task)
    {
        return getPendingTasks().Any(existingTask => existingTask.Id != task.Id && existingTask.TaskListId == task.TaskListId && canWriteTask(existingTask));
    }

    private bool shouldShowManualTaskSortOption()
    {
        return canUseManualTaskSort();
    }

    private bool canUseManualTaskSort()
    {
        return isAggregateSelected() == false;
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

    private string getTaskSortModeLabel(ETodoTaskSortMode sortMode)
    {
        switch (sortMode)
        {
            case ETodoTaskSortMode.DueDateAscending:
                return "마감 빠른순";
            case ETodoTaskSortMode.ManualOrder:
                return "직접 정렬";
            case ETodoTaskSortMode.RecentlyCreated:
                return "최근 작성순";
            default:
                return "마감 빠른순";
        }
    }

    private bool isTaskOverdue(TodoTaskItem task)
    {
        if (task.DueAt.HasValue == false || task.IsCompleted)
        {
            return false;
        }

        return ClientTimeZoneUtility.normalizeUtcDateTimeValue(task.DueAt.Value) < DateTime.UtcNow;
    }

    private bool isTaskDueToday(TodoTaskItem task)
    {
        if (task.DueAt.HasValue == false || task.IsCompleted)
        {
            return false;
        }

        DateTime displayDueAt = getDisplayDateTime(task.DueAt.Value);
        return displayDueAt.Date == getClientToday();
    }

    private string formatTaskDateTime(DateTime dateTime)
    {
        return getDisplayDateTime(dateTime).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatTaskDateTime(DateTimeOffset dateTime)
    {
        return ClientTimeZoneUtility.createClientDateTime(dateTime.UtcDateTime, mClientTimeZoneInfo).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private DateTime getDisplayDateTime(DateTime dateTime)
    {
        return ClientTimeZoneUtility.createClientDateTime(dateTime, mClientTimeZoneInfo);
    }

    private DateTime? createDueAtUtcOrNull(DateTime? localDueAtOrNull)
    {
        if (localDueAtOrNull.HasValue == false)
        {
            return null;
        }

        return ClientTimeZoneUtility.createUtcDateTime(localDueAtOrNull.Value, mClientTimeZoneInfo);
    }

    private DateTime? createEditorDueAtOrNull(DateTime? dueAtUtcOrNull)
    {
        if (dueAtUtcOrNull.HasValue == false)
        {
            return null;
        }

        return getDisplayDateTime(dueAtUtcOrNull.Value);
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
