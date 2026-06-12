using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTask = Google.Apis.Tasks.v1.Data.Task;
using GoogleTaskList = Google.Apis.Tasks.v1.Data.Tasks;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed class GoogleTasksTodoService : ITodoService
{
    private const string TASK_STATUS_COMPLETED = "completed";
    private const string TASK_STATUS_NEEDS_ACTION = "needsAction";
    private const string TASK_FIELDS = "id,title,status,position,updated,completed";
    private static readonly string[] TASK_SCOPES = new[] { GoogleTasksApi.Scope.Tasks };

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IGoogleApiCredentialFactory mCredentialFactory;
    private readonly ILogger<GoogleTasksTodoService> mLogger;
    private readonly Configuration.GoogleApiOptions mOptions;
    private readonly IChangeVersionService mChangeVersionService;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public GoogleTasksTodoService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IGoogleApiCredentialFactory credentialFactory, ILogger<GoogleTasksTodoService> logger, IOptions<Configuration.GoogleApiOptions> options, IChangeVersionService changeVersionService)
        : this(dbContextFactory, credentialFactory, logger, options, changeVersionService, new OrganizationPermissionService(dbContextFactory))
    {
    }

    public GoogleTasksTodoService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IGoogleApiCredentialFactory credentialFactory, ILogger<GoogleTasksTodoService> logger, IOptions<Configuration.GoogleApiOptions> options, IChangeVersionService changeVersionService, IOrganizationPermissionService organizationPermissionService)
    {
        mDbContextFactory = dbContextFactory;
        mCredentialFactory = credentialFactory;
        mLogger = logger;
        mOptions = options.Value;
        mChangeVersionService = changeVersionService;
        mOrganizationPermissionService = organizationPermissionService;
    }

    public async Task<IReadOnlyList<TodoTaskListReference>> GetReadableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        List<TodoTaskListReference> taskLists = (await getTaskListsAsync(userSession, ETodoTaskListAccessMode.Readable, cancellationToken)).ToList();
        if (taskLists.Count > 0)
        {
            taskLists.Insert(0, new TodoTaskListReference(TodoTaskListReference.AGGREGATE_TASK_LIST_ID, "전체", null, ETodoTaskListKind.Aggregate));
        }

        return taskLists;
    }

    public async Task<IReadOnlyList<TodoTaskListReference>> GetWritableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        IReadOnlyList<TodoTaskListReference> taskLists = await getTaskListsAsync(userSession, ETodoTaskListAccessMode.Writable, cancellationToken);
        return taskLists;
    }

    public async Task<IReadOnlyList<TodoTaskItem>> GetTasksAsync(UserSession userSession, string taskListID, CancellationToken cancellationToken)
    {
        if (TodoTaskListReference.IsAggregateTaskListID(taskListID))
        {
            return await getAggregateTasksAsync(userSession, cancellationToken);
        }

        TodoTaskListReference taskList = await getAllowedTaskListAsync(userSession, taskListID, ETodoTaskListAccessMode.Readable, cancellationToken);
        GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
        IReadOnlyList<TodoTaskItem> tasks = await getTasksAsync(tasksService, taskList.TaskListID, taskList.DisplayName, cancellationToken);
        return tasks;
    }

    private async Task<IReadOnlyList<TodoTaskItem>> getAggregateTasksAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        IReadOnlyList<TodoTaskListReference> taskLists = await getTaskListsAsync(userSession, ETodoTaskListAccessMode.Readable, cancellationToken);
        if (taskLists.Count == 0)
        {
            return new List<TodoTaskItem>();
        }

        GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
        List<Task<IReadOnlyList<TodoTaskItem>>> fetchTasks = new List<Task<IReadOnlyList<TodoTaskItem>>>();
        foreach (TodoTaskListReference taskList in taskLists)
        {
            fetchTasks.Add(getTasksAsync(tasksService, taskList.TaskListID, taskList.DisplayName, cancellationToken));
        }

        IReadOnlyList<TodoTaskItem>[] taskGroups = await Task.WhenAll(fetchTasks);
        List<TodoTaskItem> mergedTasks = new List<TodoTaskItem>();
        foreach (IReadOnlyList<TodoTaskItem> taskGroup in taskGroups)
        {
            mergedTasks.AddRange(taskGroup);
        }

        List<TodoTaskItem> orderedTasks = mergedTasks.OrderBy(task => task.IsCompleted).ThenBy(task => task.TaskListDisplayName).ThenBy(task => task.Position).ThenBy(task => task.Title).ToList();
        return orderedTasks;
    }

    private async Task<IReadOnlyList<TodoTaskItem>> getTasksAsync(GoogleTasksApi tasksService, string taskListID, string taskListDisplayName, CancellationToken cancellationToken)
    {
        List<TodoTaskItem> tasks = new List<TodoTaskItem>();
        string pageToken = string.Empty;
        do
        {
            Google.Apis.Tasks.v1.TasksResource.ListRequest request = tasksService.Tasks.List(taskListID);
            request.ShowCompleted = true;
            request.ShowDeleted = false;
            request.ShowHidden = false;
            request.MaxResults = 100;
            request.PageToken = string.IsNullOrWhiteSpace(pageToken) ? null : pageToken;
            request.Fields = "items(" + TASK_FIELDS + "),nextPageToken";
            GoogleTaskList taskList = await request.ExecuteAsync(cancellationToken);
            if (taskList.Items != null)
            {
                foreach (GoogleTask googleTask in taskList.Items)
                {
                    TodoTaskItem taskItem = createTaskItem(taskListID, googleTask);
                    taskItem.TaskListDisplayName = taskListDisplayName;
                    tasks.Add(taskItem);
                }
            }

            pageToken = getNullAsEmpty(taskList.NextPageToken);
        }
        while (string.IsNullOrWhiteSpace(pageToken) == false);

        Dictionary<string, TodoTaskMetadata> metadataByTaskID = await getMetadataByTaskIDAsync(taskListID, tasks, cancellationToken);
        applyMetadata(tasks, metadataByTaskID);
        List<TodoTaskItem> orderedTasks = tasks.OrderBy(task => task.IsCompleted).ThenBy(task => task.Position).ThenBy(task => task.Title).ToList();
        return orderedTasks;
    }

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
        keys.Add(ChangeVersionKeys.CreateTodoTaskListKey(taskListID));
        return keys;
    }

    private async Task<IReadOnlyList<TodoTaskListReference>> getTaskListsAsync(UserSession userSession, ETodoTaskListAccessMode accessMode, CancellationToken cancellationToken)
    {
        List<TodoTaskListReference> taskLists = new List<TodoTaskListReference>();
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                await addGlobalTaskListsAsync(taskLists, settingOrNull, userSession, accessMode, usesConfiguredPermissions, cancellationToken);
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.TaskListID != null && team.TaskListID != string.Empty).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName);
            if (usesConfiguredPermissions == false && accessMode == ETodoTaskListAccessMode.Writable && UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role) == false)
            {
                if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
                {
                    return taskLists;
                }

                if (userSession.TeamID.HasValue == false)
                {
                    return taskLists;
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }
            else if (usesConfiguredPermissions == false && userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    return taskLists;
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                bool canUseTaskList = await canUseTaskListAsync(userSession, team.ID, accessMode, usesConfiguredPermissions, canUseTeamTaskListFallback(userSession, team, accessMode), cancellationToken);
                if (canUseTaskList)
                {
                    addTaskListIfConfigured(taskLists, team.TaskListID, team.DisplayName, team.ID);
                }
            }
        }

        return taskLists;
    }

    private async Task addGlobalTaskListsAsync(List<TodoTaskListReference> taskLists, GlobalSetting setting, UserSession userSession, ETodoTaskListAccessMode accessMode, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        bool managementFallback = accessMode == ETodoTaskListAccessMode.Writable ? UserPermissionPolicy.CanWriteManagementWork(userSession.Role) : userSession.IsManagementRole();
        bool canUseManagementTaskList = await canUseTaskListAsync(userSession, null, accessMode, usesConfiguredPermissions, managementFallback, cancellationToken);
        if (canUseManagementTaskList)
        {
            addTaskListIfConfigured(taskLists, setting.OrganizationTaskListID, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, null);
        }

        bool responsibleFallback = accessMode == ETodoTaskListAccessMode.Writable ? UserPermissionPolicy.CanWriteResponsibleWork(userSession.Role) : userSession.IsManagementRole() || userSession.Role == Domain.EUserRole.TeamLeader;
        bool canUseResponsibleTaskList = await canUseTaskListAsync(userSession, null, accessMode, usesConfiguredPermissions, responsibleFallback, cancellationToken);
        if (canUseResponsibleTaskList)
        {
            addTaskListIfConfigured(taskLists, setting.ResponsibleTaskListID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, null);
        }
    }

    private async Task<bool> canUseTaskListAsync(UserSession userSession, int? targetTeamIDOrNull, ETodoTaskListAccessMode accessMode, bool usesConfiguredPermissions, bool fallbackPermission, CancellationToken cancellationToken)
    {
        if (usesConfiguredPermissions == false)
        {
            return fallbackPermission;
        }

        if (accessMode != ETodoTaskListAccessMode.Writable)
        {
            return await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.TODOS_READ, targetTeamIDOrNull, cancellationToken);
        }

        bool canCreate = await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.TODOS_CREATE, targetTeamIDOrNull, cancellationToken);
        if (canCreate)
        {
            return true;
        }

        bool canUpdate = await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.TODOS_UPDATE, targetTeamIDOrNull, cancellationToken);
        if (canUpdate)
        {
            return true;
        }

        return await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.TODOS_DELETE, targetTeamIDOrNull, cancellationToken);
    }

    private bool canUseTeamTaskListFallback(UserSession userSession, Team team, ETodoTaskListAccessMode accessMode)
    {
        if (accessMode == ETodoTaskListAccessMode.Writable)
        {
            if (UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role))
            {
                return true;
            }

            if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
            {
                return false;
            }
        }
        else if (userSession.IsManagementRole())
        {
            return true;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        return userSession.TeamID.Value == team.ID;
    }

    private void addTaskListIfConfigured(List<TodoTaskListReference> taskLists, string? taskListIDOrNull, string displayName, int? teamID)
    {
        if (string.IsNullOrWhiteSpace(taskListIDOrNull))
        {
            return;
        }

        bool alreadyAdded = taskLists.Any(taskList => taskList.TaskListID == taskListIDOrNull);
        if (alreadyAdded)
        {
            return;
        }

        taskLists.Add(new TodoTaskListReference(taskListIDOrNull, displayName, teamID));
    }

    private async Task ensureCanWriteTaskListAsync(UserSession userSession, string taskListID, CancellationToken cancellationToken)
    {
        if (TodoTaskListReference.IsAggregateTaskListID(taskListID))
        {
            throw new InvalidOperationException("실제 할 일 목록을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(taskListID))
        {
            throw new InvalidOperationException("할 일 목록을 선택해 주세요.");
        }

        IReadOnlyList<TodoTaskListReference> taskLists = await getTaskListsAsync(userSession, ETodoTaskListAccessMode.Writable, cancellationToken);
        bool canUseTaskList = taskLists.Any(taskList => taskList.TaskListID == taskListID);
        if (canUseTaskList == false)
        {
            throw new UnauthorizedAccessException("이 할 일 목록을 수정할 권한이 없습니다.");
        }
    }

    private async Task<TodoTaskListReference> getAllowedTaskListAsync(UserSession userSession, string taskListID, ETodoTaskListAccessMode accessMode, CancellationToken cancellationToken)
    {
        IReadOnlyList<TodoTaskListReference> taskLists = await getTaskListsAsync(userSession, accessMode, cancellationToken);
        TodoTaskListReference? taskListOrNull = taskLists.FirstOrDefault(taskList => taskList.TaskListID == taskListID);
        if (taskListOrNull == null)
        {
            throw new UnauthorizedAccessException("이 할 일 목록에 접근할 권한이 없습니다.");
        }

        return taskListOrNull;
    }

    private void validateTaskForm(TodoTaskFormModel formModel)
    {
        if (string.IsNullOrWhiteSpace(formModel.TaskListID))
        {
            throw new InvalidOperationException("할 일 목록을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.Title))
        {
            throw new InvalidOperationException("할 일을 입력해 주세요.");
        }
    }

    private void validateTaskID(string taskID)
    {
        if (string.IsNullOrWhiteSpace(taskID))
        {
            throw new InvalidOperationException("할 일 항목을 찾을 수 없습니다.");
        }
    }

    private async Task<Dictionary<string, TodoTaskMetadata>> getMetadataByTaskIDAsync(string taskListID, IReadOnlyList<TodoTaskItem> tasks, CancellationToken cancellationToken)
    {
        List<string> taskIDs = tasks.Where(task => string.IsNullOrWhiteSpace(task.ID) == false).Select(task => task.ID).Distinct().ToList();
        if (taskIDs.Count == 0)
        {
            return new Dictionary<string, TodoTaskMetadata>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<TodoTaskMetadata> metadataItems = await dbContext.TodoTaskMetadata.Where(metadata => metadata.TaskListID == taskListID && taskIDs.Contains(metadata.TaskID)).ToListAsync(cancellationToken);
            return metadataItems.ToDictionary(metadata => metadata.TaskID, metadata => metadata);
        }
    }

    private async Task<TodoTaskMetadata?> getMetadataOrNullAsync(string taskListID, string taskID, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            TodoTaskMetadata? metadataOrNull = await dbContext.TodoTaskMetadata.FirstOrDefaultAsync(metadata => metadata.TaskListID == taskListID && metadata.TaskID == taskID, cancellationToken);
            return metadataOrNull;
        }
    }

    private async Task<TodoTaskMetadata> createMetadataAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken)
    {
        TodoTaskMetadata metadata = new TodoTaskMetadata();
        metadata.TaskListID = taskListID;
        metadata.TaskID = taskID;
        metadata.CreatedByUserID = userSession.UserID;
        metadata.CreatedByDisplayName = userSession.DisplayName;
        metadata.CreatedAt = DateTime.UtcNow;

        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                TodoTaskMetadata? existingMetadataOrNull = await dbContext.TodoTaskMetadata.FirstOrDefaultAsync(existingMetadata => existingMetadata.TaskListID == taskListID && existingMetadata.TaskID == taskID, cancellationToken);
                if (existingMetadataOrNull == null)
                {
                    dbContext.TodoTaskMetadata.Add(metadata);
                }
                else
                {
                    existingMetadataOrNull.CreatedByUserID = metadata.CreatedByUserID;
                    existingMetadataOrNull.CreatedByDisplayName = metadata.CreatedByDisplayName;
                    existingMetadataOrNull.CreatedAt = metadata.CreatedAt;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to store Google Tasks metadata for task {TaskID}.", taskID);
        }

        return metadata;
    }

    private async Task deleteMetadataAsync(string taskListID, string taskID, CancellationToken cancellationToken)
    {
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                List<TodoTaskMetadata> metadataItems = await dbContext.TodoTaskMetadata.Where(metadata => metadata.TaskListID == taskListID && metadata.TaskID == taskID).ToListAsync(cancellationToken);
                dbContext.TodoTaskMetadata.RemoveRange(metadataItems);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to remove Google Tasks metadata for task {TaskID}.", taskID);
        }
    }

    private void applyMetadata(List<TodoTaskItem> tasks, Dictionary<string, TodoTaskMetadata> metadataByTaskID)
    {
        foreach (TodoTaskItem task in tasks)
        {
            TodoTaskMetadata? metadataOrNull;
            if (metadataByTaskID.TryGetValue(task.ID, out metadataOrNull))
            {
                applyMetadata(task, metadataOrNull);
            }
        }
    }

    private async Task applyMetadataAsync(TodoTaskItem task, CancellationToken cancellationToken)
    {
        TodoTaskMetadata? metadataOrNull = await getMetadataOrNullAsync(task.TaskListID, task.ID, cancellationToken);
        if (metadataOrNull == null)
        {
            return;
        }

        applyMetadata(task, metadataOrNull);
    }

    private void applyMetadata(TodoTaskItem task, TodoTaskMetadata metadata)
    {
        task.CreatedByDisplayName = metadata.CreatedByDisplayName;
        task.CreatedAt = metadata.CreatedAt;
    }

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
