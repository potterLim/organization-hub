using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        TodoTaskListId taskListId = formModel.TaskListIdValue;
        await ensureCanWriteTaskListAsync(userSession, taskListId, EResourceOperation.Create, cancellationToken);
        string requiredTaskListId = taskListId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListId)))
        {
            using (GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken))
            {
                DateTime? dueAtUtcOrNull = formModel.DueAtUtcValue.GetOptionalValueOrNull();
                GoogleTask googleTask = new GoogleTask();
                googleTask.Title = formModel.Title.Trim();
                applyTaskDueToGoogleTask(googleTask, dueAtUtcOrNull);
                Google.Apis.Tasks.v1.TasksResource.InsertRequest request = tasksService.Tasks.Insert(googleTask, requiredTaskListId);
                request.Fields = TASK_FIELDS;
                GoogleTask createdTask = await request.ExecuteAsync(cancellationToken);
                TodoTaskItem createdItem = createTaskItem(taskListId, createdTask);
                TodoTaskMetadata createdMetadata = await ResourceCreationGuard.PersistMetadataAsync(
                    () => createMetadataAsync(userSession, taskListId, TodoTaskId.CreateOptional(createdItem.Id), dueAtUtcOrNull, cancellationToken),
                    rollbackToken => tasksService.Tasks.Delete(requiredTaskListId, createdItem.Id).ExecuteAsync(rollbackToken),
                    requiredTaskListId + "/" + createdItem.Id, mLogger);
                applyMetadata(createdItem, createdMetadata);
                await applyItemPermissionsAsync(userSession, new TodoTaskItem[] { createdItem }, false, cancellationToken);
                return createdItem;
            }
        }
    }

    public async Task<TodoTaskItem> UpdateTaskAsync(UserSession userSession, TodoTaskFormModel formModel, CancellationToken cancellationToken)
    {
        validateTaskForm(formModel);
        TodoTaskListId taskListId = formModel.TaskListIdValue;
        TodoTaskId taskId = formModel.TaskIdValue;
        validateTaskId(taskId);
        await ensureCanModifyTaskAsync(userSession, taskListId, taskId, EResourceOperation.Update, cancellationToken);
        string requiredTaskListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListId)))
        {
            using (GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken))
            {
                DateTime? dueAtUtcOrNull = formModel.DueAtUtcValue.GetOptionalValueOrNull();
                GoogleTask googleTask = new GoogleTask();
                googleTask.Title = formModel.Title.Trim();
                applyTaskDueToGoogleTask(googleTask, dueAtUtcOrNull);
                Google.Apis.Tasks.v1.TasksResource.PatchRequest request = tasksService.Tasks.Patch(googleTask, requiredTaskListId, requiredTaskId);
                request.Fields = TASK_FIELDS;
                request.ModifyRequest = message => prepareTaskEdit(message, formModel, googleTask.Title);
                ResourceEditVersion.Validate(formModel.ETag);
                TodoDeadlineChange deadlineChange = await prepareDeadlineChangeAsync(formModel, cancellationToken);
                GoogleTask updatedTask;
                try
                {
                    updatedTask = await ResourceEditVersion.ExecuteAsync(() => request.ExecuteAsync(cancellationToken));
                }
                catch (ResourceEditConflictException)
                {
                    await removeDeadlineChangeAsync(deadlineChange.Id, CancellationToken.None);
                    throw;
                }
                catch (Google.GoogleApiException exception) when ((int)exception.HttpStatusCode >= 400 && (int)exception.HttpStatusCode < 500)
                {
                    await removeDeadlineChangeAsync(deadlineChange.Id, CancellationToken.None);
                    throw;
                }
                TodoTaskItem updatedItem = createTaskItem(taskListId, updatedTask);
                try
                {
                    await confirmDeadlineChangeAsync(deadlineChange, updatedItem.ETag, CancellationToken.None);
                    await finishDeadlineChangeAsync(deadlineChange, updatedItem.ETag, cancellationToken);
                }
                catch (Exception exception)
                {
                    mLogger.LogWarning(exception, "Deadline change {ChangeId} will be recovered from Google.", deadlineChange.Id);
                    throw new ResourceSynchronizationException(exception, string.IsNullOrEmpty(deadlineChange.ConfirmedGoogleETag) == false);
                }
                await applyMetadataAsync(updatedItem, cancellationToken);
                await applyItemPermissionsAsync(userSession, new TodoTaskItem[] { updatedItem }, false, cancellationToken);
                return updatedItem;
            }
        }
    }

    public async Task<TodoTaskItem> CompleteTaskAsync(
        UserSession userSession,
        TodoTaskListId taskListId,
        TodoTaskId taskId,
        CancellationToken cancellationToken
    )
    {
        return await setTaskCompletionStateAsync(userSession, taskListId, taskId, ETodoTaskCompletionState.Completed, cancellationToken);
    }

    public async Task<TodoTaskItem> ReopenTaskAsync(
        UserSession userSession,
        TodoTaskListId taskListId,
        TodoTaskId taskId,
        CancellationToken cancellationToken
    )
    {
        return await setTaskCompletionStateAsync(userSession, taskListId, taskId, ETodoTaskCompletionState.NeedsAction, cancellationToken);
    }

    public async Task DeleteTaskAsync(UserSession userSession, TodoTaskListId taskListId, TodoTaskId taskId, string expectedETag, CancellationToken cancellationToken)
    {
        validateTaskId(taskId);
        await ensureCanModifyTaskAsync(userSession, taskListId, taskId, EResourceOperation.Delete, cancellationToken);
        string requiredTaskListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListId)))
        {
            using (GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken))
            {
                Google.Apis.Tasks.v1.TasksResource.DeleteRequest request = tasksService.Tasks.Delete(requiredTaskListId, requiredTaskId);
                request.ModifyRequest = message => ResourceEditVersion.Apply(message, expectedETag);
                await ResourceDeletionConfirmation.ExecuteAsync(
                    () => ResourceEditVersion.ExecuteAsync(() => request.ExecuteAsync(cancellationToken)),
                    () => ResourceDeletionConfirmation.IsDeletedAsync(async () => (await tasksService.Tasks.Get(requiredTaskListId, requiredTaskId).ExecuteAsync(cancellationToken)).Deleted == true),
                    cancellationToken);
                await deleteMetadataAsync(taskListId, taskId, cancellationToken);
            }
        }
    }

    public async Task<TodoTaskItem> MoveTaskAsync(
        UserSession userSession,
        TodoTaskListId taskListId,
        TodoTaskId taskId,
        TodoTaskId previousTaskId,
        CancellationToken cancellationToken)
    {
        validateTaskId(taskId);
        await ensureCanModifyTaskAsync(userSession, taskListId, taskId, EResourceOperation.Update, cancellationToken);
        string requiredTaskListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListId)))
        {
            using (GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken))
            {
                Google.Apis.Tasks.v1.TasksResource.MoveRequest request = tasksService.Tasks.Move(requiredTaskListId, requiredTaskId);
                request.Previous = previousTaskId.HasValue ? previousTaskId.GetRequiredValue() : null;
                request.Fields = TASK_FIELDS;
                GoogleTask movedTask = await request.ExecuteAsync(cancellationToken);
                TodoTaskItem movedItem = createTaskItem(taskListId, movedTask);
                await applyMetadataAsync(movedItem, cancellationToken);
                await applyItemPermissionsAsync(userSession, new TodoTaskItem[] { movedItem }, false, cancellationToken);
                return movedItem;
            }
        }
    }


    private async Task<TodoTaskItem> setTaskCompletionStateAsync(
        UserSession userSession,
        TodoTaskListId taskListId,
        TodoTaskId taskId,
        ETodoTaskCompletionState completionState,
        CancellationToken cancellationToken)
    {
        validateTaskId(taskId);
        await ensureCanModifyTaskAsync(userSession, taskListId, taskId, EResourceOperation.Update, cancellationToken);
        string requiredTaskListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createTodoChangeKeys(taskListId)))
        {
            using (GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken))
            {
                GoogleTask googleTask = new GoogleTask();
                googleTask.Status = getGoogleTaskStatus(completionState);
                Google.Apis.Tasks.v1.TasksResource.PatchRequest request = tasksService.Tasks.Patch(googleTask, requiredTaskListId, requiredTaskId);
                request.Fields = TASK_FIELDS;
                GoogleTask updatedTask = await request.ExecuteAsync(cancellationToken);
                TodoTaskItem updatedItem = createTaskItem(taskListId, updatedTask);
                if (getCompletionState(updatedItem) != completionState)
                {
                    updatedItem = await getTaskAfterCompletionSettlesAsync(tasksService, taskListId, taskId, completionState, cancellationToken);
                }

                await applyMetadataAsync(updatedItem, cancellationToken);
                await applyItemPermissionsAsync(userSession, new TodoTaskItem[] { updatedItem }, false, cancellationToken);
                return updatedItem;
            }
        }
    }
    private static IReadOnlyList<ChangeVersionKey> createTodoChangeKeys(TodoTaskListId taskListId)
    {
        List<ChangeVersionKey> changeVersionKeys = new List<ChangeVersionKey>();
        changeVersionKeys.Add(ChangeVersionKeys.TASKS);
        changeVersionKeys.Add(ChangeVersionKeys.CreateTodoTaskListKey(taskListId));
        return changeVersionKeys;
    }

    private void applyTaskDueToGoogleTask(GoogleTask googleTask, DateTime? dueAtOrNull)
    {
        string? googleDueOrNull = formatGoogleTaskDueOrNull(dueAtOrNull);
        if (string.IsNullOrWhiteSpace(googleDueOrNull))
        {
            return;
        }

        googleTask.Due = googleDueOrNull;
    }

    private static void prepareTaskEdit(HttpRequestMessage message, TodoTaskFormModel form, string title)
    {
        ResourceEditVersion.Apply(message, form.ETag);
        if (form.DueAtUtc.HasValue == false)
        {
            applyClearedDueContent(message, title);
        }
    }

    private static void applyClearedDueContent(HttpRequestMessage message, string title)
    {
        JsonObject content = new JsonObject();
        content["title"] = title;
        content["due"] = null;
        message.Content?.Dispose();
        message.Content = new StringContent(content.ToJsonString(), Encoding.UTF8, "application/json");
    }
}
