using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
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
}
