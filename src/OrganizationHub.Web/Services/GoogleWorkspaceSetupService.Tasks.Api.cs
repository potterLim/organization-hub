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
    private const string GOOGLE_TASKS_ASSIGNMENT_SUFFIX = " Google Tasks 목록";
    private const string LEGACY_GOOGLE_TASKS_LIST_SUFFIX = " Google Tasks";
    private const string STANDARD_TASK_LIST_SUFFIX = " 할 일";

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
            request.MaxResults = GOOGLE_TASK_LIST_PAGE_SIZE;
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

        GoogleTaskListData? legacyTaskListOrNull = findLegacyGoogleTaskListOrNull(googleTaskLists, standardName);
        if (legacyTaskListOrNull != null)
        {
            GoogleTaskListData renamedTaskList = await renameGoogleTaskListAsync(tasksService, legacyTaskListOrNull, standardName, cancellationToken);
            summary.ReusedCount = summary.ReusedCount + 1;
            summary.RenamedCount = summary.RenamedCount + 1;
            replaceGoogleTaskList(googleTaskLists, renamedTaskList);
            return renamedTaskList;
        }

        GoogleTaskListData newTaskList = new GoogleTaskListData();
        newTaskList.Title = standardName;
        Google.Apis.Tasks.v1.TasklistsResource.InsertRequest request = tasksService.Tasklists.Insert(newTaskList);
        GoogleTaskListData createdTaskList = await request.ExecuteAsync(cancellationToken);
        googleTaskLists.Add(createdTaskList);
        summary.CreatedCount = summary.CreatedCount + 1;
        return createdTaskList;
    }

    private GoogleTaskListData? findLegacyGoogleTaskListOrNull(List<GoogleTaskListData> googleTaskLists, string standardName)
    {
        string legacyName = createLegacyGoogleTaskListName(standardName);
        if (string.IsNullOrWhiteSpace(legacyName))
        {
            return null;
        }

        return googleTaskLists.FirstOrDefault(taskList => taskList.Title == legacyName);
    }

    private async Task<GoogleTaskListData> renameGoogleTaskListAsync(
        GoogleTasksApi tasksService,
        GoogleTaskListData taskList,
        string standardName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskList.Id))
        {
            throw new InvalidOperationException(standardName + " 목록으로 이름을 바꿀 Google Tasks 목록 ID가 없습니다.");
        }

        GoogleTaskListData updateTaskList = new GoogleTaskListData();
        updateTaskList.Id = taskList.Id;
        updateTaskList.Title = standardName;
        Google.Apis.Tasks.v1.TasklistsResource.UpdateRequest request = tasksService.Tasklists.Update(updateTaskList, taskList.Id);
        return await request.ExecuteAsync(cancellationToken);
    }

    private void replaceGoogleTaskList(List<GoogleTaskListData> googleTaskLists, GoogleTaskListData updatedTaskList)
    {
        int listIndex = googleTaskLists.FindIndex(taskList => taskList.Id == updatedTaskList.Id);
        if (listIndex < 0)
        {
            googleTaskLists.Add(updatedTaskList);
            return;
        }

        googleTaskLists[listIndex] = updatedTaskList;
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
        if (displayName.EndsWith(GOOGLE_TASKS_ASSIGNMENT_SUFFIX, StringComparison.Ordinal))
        {
            displayName = displayName.Substring(0, displayName.Length - GOOGLE_TASKS_ASSIGNMENT_SUFFIX.Length);
        }

        if (displayName.EndsWith(STANDARD_TASK_LIST_SUFFIX, StringComparison.Ordinal))
        {
            return displayName;
        }

        return displayName + STANDARD_TASK_LIST_SUFFIX;
    }

    private string createLegacyGoogleTaskListName(string standardName)
    {
        if (standardName.EndsWith(STANDARD_TASK_LIST_SUFFIX, StringComparison.Ordinal) == false)
        {
            return string.Empty;
        }

        string baseName = standardName.Substring(0, standardName.Length - STANDARD_TASK_LIST_SUFFIX.Length);
        if (baseName == OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME || baseName == OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME)
        {
            return string.Empty;
        }

        return baseName + LEGACY_GOOGLE_TASKS_LIST_SUFFIX;
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
        List<string> visibleMessages = failureMessages.Take(VISIBLE_FAILURE_MESSAGE_COUNT).ToList();
        string message = "일부 Google Tasks 목록을 저장하지 못했습니다. " + string.Join(" / ", visibleMessages);
        if (failureMessages.Count > visibleMessages.Count)
        {
            message = message + " 외 " + (failureMessages.Count - visibleMessages.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) + "건";
        }

        return message;
    }
}
