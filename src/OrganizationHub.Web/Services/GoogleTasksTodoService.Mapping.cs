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

    private async Task<TodoTaskItem> getTaskAfterCompletionSettlesAsync(GoogleTasksApi tasksService, string taskListID, string taskID, ETodoTaskCompletionState expectedCompletionState, CancellationToken cancellationToken)
    {
        TodoTaskItem latestTask = new TodoTaskItem();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Google.Apis.Tasks.v1.TasksResource.GetRequest getRequest = tasksService.Tasks.Get(taskListID, taskID);
            getRequest.Fields = TASK_FIELDS;
            GoogleTask googleTask = await getRequest.ExecuteAsync(cancellationToken);
            latestTask = createTaskItem(taskListID, googleTask);
            if (getCompletionState(latestTask) == expectedCompletionState)
            {
                return latestTask;
            }

            await Task.Delay(700, cancellationToken);
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

    private TodoTaskItem createTaskItem(string taskListID, GoogleTask googleTask)
    {
        TodoTaskItem item = new TodoTaskItem();
        item.ID = getNullAsEmpty(googleTask.Id);
        item.TaskListID = taskListID;
        item.Title = getNullAsEmpty(googleTask.Title);
        item.Position = getNullAsEmpty(googleTask.Position);
        item.IsCompleted = googleTask.Status == TASK_STATUS_COMPLETED;
        item.UpdatedAt = parseGoogleDateTime(googleTask.Updated);
        item.CompletedAt = parseGoogleDateTime(googleTask.Completed);
        return item;
    }

    private DateTimeOffset? parseGoogleDateTime(string? valueOrNull)
    {
        if (string.IsNullOrWhiteSpace(valueOrNull))
        {
            return null;
        }

        DateTimeOffset parsedValue;
        bool isParsed = DateTimeOffset.TryParse(valueOrNull, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsedValue);
        if (isParsed == false)
        {
            return null;
        }

        return parsedValue;
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }
}
