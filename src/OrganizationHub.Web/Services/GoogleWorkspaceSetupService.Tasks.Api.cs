using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Google.Apis.Services;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTaskListData = Google.Apis.Tasks.v1.Data.TaskList;
using GoogleTaskListsData = Google.Apis.Tasks.v1.Data.TaskLists;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private async Task<GoogleTasksApi> createTasksServiceAsync(CancellationToken cancellationToken)
    {
        if (mCredentialFactoryOrNull == null || mOptionsOrNull == null)
        {
            throw new InvalidOperationException("Google Tasks 목록 생성 서비스를 사용할 수 없습니다.");
        }

        IConfigurableHttpClientInitializer credential = await mCredentialFactoryOrNull.CreateHttpClientInitializerAsync(TASK_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptionsOrNull.ApplicationName;
        return new GoogleTasksApi(initializer);
    }

    private async Task<List<GoogleTaskListData>> listGoogleTaskListsAsync(GoogleTasksApi tasksService, CancellationToken cancellationToken)
    {
        List<GoogleTaskListData> taskLists = new List<GoogleTaskListData>();
        string pageToken = string.Empty;
        do
        {
            Google.Apis.Tasks.v1.TasklistsResource.ListRequest request = tasksService.Tasklists.List();
            request.MaxResults = 100;
            request.PageToken = string.IsNullOrWhiteSpace(pageToken) ? null : pageToken;
            request.Fields = "items(id,title),nextPageToken";
            GoogleTaskListsData response = await request.ExecuteAsync(cancellationToken);
            if (response.Items != null)
            {
                foreach (GoogleTaskListData taskList in response.Items)
                {
                    taskLists.Add(taskList);
                }
            }

            pageToken = getTextOrEmpty(response.NextPageToken);
        }
        while (string.IsNullOrWhiteSpace(pageToken) == false);

        return taskLists;
    }

    private async Task<GoogleTaskListData> getOrCreateGoogleTaskListAsync(
        GoogleTasksApi tasksService,
        List<GoogleTaskListData> googleTaskLists,
        string standardName,
        GoogleTaskListSetupSummary summary,
        CancellationToken cancellationToken)
    {
        GoogleTaskListData? existingTaskListOrNull = googleTaskLists.FirstOrDefault(taskList => taskList.Title == standardName);
        if (existingTaskListOrNull != null)
        {
            summary.ReusedCount = summary.ReusedCount + 1;
            return existingTaskListOrNull;
        }

        GoogleTaskListData newTaskList = new GoogleTaskListData();
        newTaskList.Title = standardName;
        Google.Apis.Tasks.v1.TasklistsResource.InsertRequest request = tasksService.Tasklists.Insert(newTaskList);
        GoogleTaskListData createdTaskList = await request.ExecuteAsync(cancellationToken);
        googleTaskLists.Add(createdTaskList);
        summary.CreatedCount = summary.CreatedCount + 1;
        return createdTaskList;
    }

    private string getStandardTaskListName(GoogleTaskListAssignmentOption option)
    {
        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Organization)
        {
            return OrganizationHubWorkspaceDefaults.ORGANIZATION_TASK_LIST_NAME;
        }

        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Responsible)
        {
            return OrganizationHubWorkspaceDefaults.RESPONSIBLE_TASK_LIST_NAME;
        }

        string displayName = option.DisplayName;
        if (displayName.EndsWith(" Google Tasks 목록", StringComparison.Ordinal))
        {
            displayName = displayName.Substring(0, displayName.Length - " Google Tasks 목록".Length);
        }

        return displayName + " Google Tasks";
    }

    private void clearTaskListCache()
    {
        if (mMemoryCacheOrNull == null)
        {
            return;
        }

        mMemoryCacheOrNull.Remove(TASK_LIST_CACHE_KEY);
    }

    private string createGoogleTasksSetupErrorMessage(Exception exception)
    {
        return GoogleApiExceptionMessageFormatter.CreateGoogleTasksMessage(exception, "기본 Google Tasks 목록을 만들지 못했습니다.");
    }

    private string createTaskListSetupFailureMessage(IReadOnlyList<string> failureMessages)
    {
        List<string> visibleMessages = failureMessages.Take(3).ToList();
        string message = "일부 Google Tasks 목록을 저장하지 못했습니다. " + string.Join(" / ", visibleMessages);
        if (failureMessages.Count > visibleMessages.Count)
        {
            message = message + " 외 " + (failureMessages.Count - visibleMessages.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) + "건";
        }

        return message;
    }
}
