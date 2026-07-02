using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
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
        DateTime? selectedDueAtOrNull = mNewTaskDueAt;
        DateTime? dueAtUtcOrNull = createDueAtUtcOrNull(selectedDueAtOrNull);
        TodoTaskItem optimisticTask = createOptimisticTask(taskTitle, dueAtUtcOrNull);
        try
        {
            prependTask(optimisticTask);
            mNewTaskTitle = string.Empty;
            mNewTaskDueAt = null;
            mIsNewTaskDuePickerOpen = false;
            mMessage = string.Empty;
            await InvokeAsync(StateHasChanged);
            TodoTaskFormModel formModel = new TodoTaskFormModel();
            formModel.TaskListId = mSelectedTaskListId;
            formModel.Title = taskTitle;
            formModel.DueAtUtc = dueAtUtcOrNull;
            TodoTaskItem createdTask = await TodoService.CreateTaskAsync(CurrentUserSessionOrNull, formModel, CancellationToken.None);
            removeTaskFromLocalList(TodoTaskListId.CreateOptional(optimisticTask.TaskListId), TodoTaskId.CreateOptional(optimisticTask.Id));
            prependTask(createdTask);
            recordLoadedChangeSnapshot();
        }
        catch (Exception exception)
        {
            removeTaskFromLocalList(TodoTaskListId.CreateOptional(optimisticTask.TaskListId), TodoTaskId.CreateOptional(optimisticTask.Id));
            if (string.IsNullOrWhiteSpace(mNewTaskTitle))
            {
                mNewTaskTitle = taskTitle;
            }

            mNewTaskDueAt = selectedDueAtOrNull;
            showError(createTodoErrorMessage(exception));
        }

        mIsSaving = false;
    }

    private Task setNewTaskDueAtAsync(DateTime? dueAtOrNull)
    {
        mNewTaskDueAt = dueAtOrNull;
        return Task.CompletedTask;
    }

    private Task setNewTaskDuePickerOpenAsync(bool isOpen)
    {
        mIsNewTaskDuePickerOpen = isOpen;
        return Task.CompletedTask;
    }
}
