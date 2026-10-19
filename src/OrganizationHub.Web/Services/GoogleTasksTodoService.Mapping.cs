using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Google.Apis.Services;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTask = Google.Apis.Tasks.v1.Data.Task;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    private async Task<GoogleTasksApi> createTasksServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(TASK_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new GoogleTasksApi(initializer);
    }

    private async Task<TodoTaskItem> getTaskAfterCompletionSettlesAsync(
        GoogleTasksApi tasksService,
        TodoTaskListId taskListId,
        TodoTaskId taskId,
        ETodoTaskCompletionState expectedCompletionState,
        CancellationToken cancellationToken
    )
    {
        string requiredTaskListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        TodoTaskItem latestTask = new TodoTaskItem();
        for (int attempt = 0; attempt < TASK_COMPLETION_SETTLE_RETRY_COUNT; ++attempt)
        {
            Google.Apis.Tasks.v1.TasksResource.GetRequest getRequest = tasksService.Tasks.Get(requiredTaskListId, requiredTaskId);
            getRequest.Fields = TASK_FIELDS;
            GoogleTask googleTask = await getRequest.ExecuteAsync(cancellationToken);
            latestTask = createTaskItem(taskListId, googleTask);
            if (getCompletionState(latestTask) == expectedCompletionState)
            {
                return latestTask;
            }

            await Task.Delay(TASK_COMPLETION_SETTLE_DELAY_MILLISECONDS, cancellationToken);
        }

        string expectedStatus = getGoogleTaskStatus(expectedCompletionState);
        throw new InvalidOperationException("Google Tasks did not confirm the requested status: " + expectedStatus);
    }

    private static ETodoTaskCompletionState getCompletionState(TodoTaskItem task)
    {
        return task.IsCompleted ? ETodoTaskCompletionState.Completed : ETodoTaskCompletionState.NeedsAction;
    }

    private static string getGoogleTaskStatus(ETodoTaskCompletionState completionState)
    {
        return completionState == ETodoTaskCompletionState.Completed ? TASK_STATUS_COMPLETED : TASK_STATUS_NEEDS_ACTION;
    }

    private TodoTaskItem createTaskItem(TodoTaskListId taskListId, GoogleTask googleTask)
    {
        TodoTaskItem item = new TodoTaskItem();
        item.Id = getTextOrEmpty(googleTask.Id);
        item.ETag = getTextOrEmpty(googleTask.ETag);
        item.TaskListId = taskListId.GetRequiredValue();
        item.Title = getTextOrEmpty(googleTask.Title);
        item.Position = getTextOrEmpty(googleTask.Position);
        item.IsCompleted = googleTask.Status == TASK_STATUS_COMPLETED;
        item.UpdatedAt = parseGoogleDateTimeOrNull(googleTask.Updated);
        item.CompletedAt = parseGoogleDateTimeOrNull(googleTask.Completed);
        item.DueAt = parseGoogleTaskDueOrNull(googleTask.Due);
        return item;
    }

    private DateTime? parseGoogleTaskDueOrNull(string? dueTextOrNull)
    {
        DateTimeOffset? parsedDueAtOrNull = parseGoogleDateTimeOrNull(dueTextOrNull);
        if (parsedDueAtOrNull.HasValue == false)
        {
            return null;
        }

        return parsedDueAtOrNull.Value.UtcDateTime;
    }

    private string? formatGoogleTaskDueOrNull(DateTime? dueAtOrNull)
    {
        if (dueAtOrNull.HasValue == false)
        {
            return null;
        }

        DateTime utcDueAt = TodoDueAtUtc.CreateRequiredUtc(dueAtOrNull.Value).GetRequiredValue();
        return utcDueAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    private DateTimeOffset? parseGoogleDateTimeOrNull(string? dateTimeTextOrNull)
    {
        if (string.IsNullOrWhiteSpace(dateTimeTextOrNull))
        {
            return null;
        }

        DateTimeOffset parsedValue;
        bool isParsed = DateTimeOffset.TryParse(dateTimeTextOrNull, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsedValue);
        if (isParsed == false)
        {
            return null;
        }

        return parsedValue;
    }

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }
}
