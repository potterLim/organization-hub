using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTask = Google.Apis.Tasks.v1.Data.Task;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    public async Task<TodoTaskItem> CreateTaskAsync(UserSession userSession, TodoTaskFormModel formModel, CancellationToken cancellationToken)
    {
        validateTaskForm(formModel);
        await ensureCanWriteTaskListAsync(userSession, formModel.TaskListID, cancellationToken);
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(formModel.TaskListID)))
        {
            GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
            GoogleTask googleTask = new GoogleTask();
            googleTask.Title = formModel.Title.Trim();
            Google.Apis.Tasks.v1.TasksResource.InsertRequest request = tasksService.Tasks.Insert(googleTask, formModel.TaskListID);
            request.Fields = TASK_FIELDS;
            GoogleTask createdTask = await request.ExecuteAsync(cancellationToken);
            TodoTaskItem createdItem = createTaskItem(formModel.TaskListID, createdTask);
            TodoTaskMetadata createdMetadata = await createMetadataAsync(userSession, formModel.TaskListID, createdItem.ID, cancellationToken);
            applyMetadata(createdItem, createdMetadata);
            return createdItem;
        }
    }

    public async Task<TodoTaskItem> UpdateTaskAsync(UserSession userSession, TodoTaskFormModel formModel, CancellationToken cancellationToken)
    {
        validateTaskForm(formModel);
        validateTaskID(formModel.TaskID);
        await ensureCanWriteTaskListAsync(userSession, formModel.TaskListID, cancellationToken);
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(formModel.TaskListID)))
        {
            GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
            GoogleTask googleTask = new GoogleTask();
            googleTask.Title = formModel.Title.Trim();
            Google.Apis.Tasks.v1.TasksResource.PatchRequest request = tasksService.Tasks.Patch(googleTask, formModel.TaskListID, formModel.TaskID);
            request.Fields = TASK_FIELDS;
            GoogleTask updatedTask = await request.ExecuteAsync(cancellationToken);
            TodoTaskItem updatedItem = createTaskItem(formModel.TaskListID, updatedTask);
            await applyMetadataAsync(updatedItem, cancellationToken);
            return updatedItem;
        }
    }

    public async Task<TodoTaskItem> CompleteTaskAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken)
    {
        return await setTaskCompletionStateAsync(userSession, taskListID, taskID, ETodoTaskCompletionState.Completed, cancellationToken);
    }

    public async Task<TodoTaskItem> ReopenTaskAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken)
    {
        return await setTaskCompletionStateAsync(userSession, taskListID, taskID, ETodoTaskCompletionState.NeedsAction, cancellationToken);
    }

    private async Task<TodoTaskItem> setTaskCompletionStateAsync(UserSession userSession, string taskListID, string taskID, ETodoTaskCompletionState completionState, CancellationToken cancellationToken)
    {
        validateTaskID(taskID);
        await ensureCanWriteTaskListAsync(userSession, taskListID, cancellationToken);
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListID)))
        {
            GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
            GoogleTask googleTask = new GoogleTask();
            googleTask.Status = getGoogleTaskStatus(completionState);
            Google.Apis.Tasks.v1.TasksResource.PatchRequest request = tasksService.Tasks.Patch(googleTask, taskListID, taskID);
            request.Fields = TASK_FIELDS;
            GoogleTask updatedTask = await request.ExecuteAsync(cancellationToken);
            TodoTaskItem updatedItem = createTaskItem(taskListID, updatedTask);
            if (getCompletionState(updatedItem) != completionState)
            {
                updatedItem = await getTaskAfterCompletionSettlesAsync(tasksService, taskListID, taskID, completionState, cancellationToken);
            }

            await applyMetadataAsync(updatedItem, cancellationToken);
            return updatedItem;
        }
    }

    public async Task DeleteTaskAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken)
    {
        validateTaskID(taskID);
        await ensureCanWriteTaskListAsync(userSession, taskListID, cancellationToken);
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListID)))
        {
            GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
            Google.Apis.Tasks.v1.TasksResource.DeleteRequest request = tasksService.Tasks.Delete(taskListID, taskID);
            await request.ExecuteAsync(cancellationToken);
            await deleteMetadataAsync(taskListID, taskID, cancellationToken);
        }
    }

    public async Task<TodoTaskItem> MoveTaskAsync(UserSession userSession, string taskListID, string taskID, string previousTaskID, CancellationToken cancellationToken)
    {
        validateTaskID(taskID);
        await ensureCanWriteTaskListAsync(userSession, taskListID, cancellationToken);
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListID)))
        {
            GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
            Google.Apis.Tasks.v1.TasksResource.MoveRequest request = tasksService.Tasks.Move(taskListID, taskID);
            request.Previous = string.IsNullOrWhiteSpace(previousTaskID) ? null : previousTaskID;
            request.Fields = TASK_FIELDS;
            GoogleTask movedTask = await request.ExecuteAsync(cancellationToken);
            TodoTaskItem movedItem = createTaskItem(taskListID, movedTask);
            await applyMetadataAsync(movedItem, cancellationToken);
            return movedItem;
        }
    }

    private static IReadOnlyList<string> createTodoChangeKeys(string taskListID)
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.TODO);
        keys.Add(ChangeVersionKeys.CreateTodoTaskListKey(new TodoTaskListID(taskListID)));
        return keys;
    }
}
