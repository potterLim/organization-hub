using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTaskListData = Google.Apis.Tasks.v1.Data.TaskList;
using GoogleTaskListsData = Google.Apis.Tasks.v1.Data.TaskLists;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private const string TASK_LIST_CACHE_KEY = "OrganizationHub:GoogleWorkspace:TaskLists";
    private static readonly string[] TASK_SCOPES = new[] { GoogleTasksApi.Scope.Tasks };

    public async Task<IReadOnlyList<GoogleTaskListAssignmentOption>> GetTaskListAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        List<GoogleTaskListAssignmentOption> options = new List<GoogleTaskListAssignmentOption>();
        options.Add(createTaskListAssignmentOption("Management", "조직 공용 Google Tasks 목록", EGoogleTaskListAssignmentKind.Organization, null));
        options.Add(createTaskListAssignmentOption("Leader", "책임자 Google Tasks 목록", EGoogleTaskListAssignmentKind.Leader, null));

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            foreach (Team team in teams)
            {
                options.Add(createTaskListAssignmentOption("Team:" + team.ID.ToString(System.Globalization.CultureInfo.InvariantCulture), team.DisplayName + " Google Tasks 목록", EGoogleTaskListAssignmentKind.Team, team.ID));
            }
        }

        return options;
    }

    public async Task<ServiceResultOfT<GoogleTaskListSetupSummary>> EnsureStandardTaskListsAsync(CancellationToken cancellationToken)
    {
        if (mCredentialFactoryOrNull == null || mOptionsOrNull == null)
        {
            return ServiceResultOfT<GoogleTaskListSetupSummary>.Fail("Google Tasks 목록 생성 서비스를 사용할 수 없습니다.");
        }

        GoogleTaskListSetupSummary summary = new GoogleTaskListSetupSummary();
        List<string> failureMessages = new List<string>();
        try
        {
            GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
            List<GoogleTaskListData> googleTaskLists = await listGoogleTaskListsAsync(tasksService, cancellationToken);
            IReadOnlyList<GoogleTaskListAssignmentOption> options = await GetTaskListAssignmentOptionsAsync(cancellationToken);
            foreach (GoogleTaskListAssignmentOption option in options)
            {
                summary.TargetCount = summary.TargetCount + 1;
                string standardName = getStandardTaskListName(option);
                GoogleTaskListData taskList = await getOrCreateGoogleTaskListAsync(tasksService, googleTaskLists, standardName, summary, cancellationToken);
                if (string.IsNullOrWhiteSpace(taskList.Id))
                {
                    failureMessages.Add(standardName + " 목록 ID를 받지 못했습니다.");
                    continue;
                }

                ServiceResult assignResult = await AssignTaskListAsync(option.TargetKey, taskList.Id, cancellationToken);
                if (assignResult.IsSuccess)
                {
                    summary.AssignedCount = summary.AssignedCount + 1;
                    continue;
                }

                failureMessages.Add(standardName + " 목록을 저장하지 못했습니다. " + assignResult.ErrorMessage);
            }

            clearTaskListCache();
        }
        catch (Exception exception)
        {
            return ServiceResultOfT<GoogleTaskListSetupSummary>.Fail(createGoogleTasksSetupErrorMessage(exception));
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleTaskListSetupSummary>.Fail(createTaskListSetupFailureMessage(failureMessages));
        }

        return ServiceResultOfT<GoogleTaskListSetupSummary>.Succeed(summary);
    }

    public async Task<ServiceResult> AssignTaskListAsync(string targetKey, string taskListID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return ServiceResult.Fail("저장 대상을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(taskListID))
        {
            return ServiceResult.Fail("Google Tasks 목록을 선택해 주세요.");
        }

        IReadOnlyList<GoogleTaskListAssignmentOption> options = await GetTaskListAssignmentOptionsAsync(cancellationToken);
        GoogleTaskListAssignmentOption? optionOrNull = options.FirstOrDefault(option => option.TargetKey == targetKey);
        if (optionOrNull == null)
        {
            return ServiceResult.Fail("저장 대상을 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (optionOrNull.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Organization)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.OrganizationTaskListID = taskListID.Trim();
            }
            else if (optionOrNull.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Leader)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.ResponsibleTaskListID = taskListID.Trim();
            }
            else
            {
                if (optionOrNull.TeamID.HasValue == false)
                {
                    return ServiceResult.Fail("팀 Google Tasks 목록 대상을 찾을 수 없습니다.");
                }

                int teamID = optionOrNull.TeamID.Value;
                Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == teamID, cancellationToken);
                if (teamOrNull == null)
                {
                    return ServiceResult.Fail("팀을 찾을 수 없습니다.");
                }

                teamOrNull.TaskListID = taskListID.Trim();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

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

            pageToken = getNullAsEmpty(response.NextPageToken);
        }
        while (string.IsNullOrWhiteSpace(pageToken) == false);

        return taskLists;
    }

    private async Task<GoogleTaskListData> getOrCreateGoogleTaskListAsync(GoogleTasksApi tasksService, List<GoogleTaskListData> googleTaskLists, string standardName, GoogleTaskListSetupSummary summary, CancellationToken cancellationToken)
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

        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Leader)
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

    private GoogleTaskListAssignmentOption createTaskListAssignmentOption(string targetKey, string displayName, EGoogleTaskListAssignmentKind taskListAssignmentKind, int? teamID)
    {
        GoogleTaskListAssignmentOption option = new GoogleTaskListAssignmentOption();
        option.TargetKey = targetKey;
        option.DisplayName = displayName;
        option.TaskListAssignmentKind = taskListAssignmentKind;
        option.TeamID = teamID;
        return option;
    }
}
