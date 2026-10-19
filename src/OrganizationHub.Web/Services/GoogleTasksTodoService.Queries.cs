using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTask = Google.Apis.Tasks.v1.Data.Task;
using GoogleTaskList = Google.Apis.Tasks.v1.Data.Tasks;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    public async Task<IReadOnlyList<TodoTaskListReference>> GetReadableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        List<TodoTaskListReference> taskLists = (await getTaskListsAsync(userSession, ETodoTaskListAccessMode.Readable, cancellationToken)).ToList();
        if (taskLists.Count > 0)
        {
            taskLists.Insert(0, new TodoTaskListReference(TodoTaskListId.AllTasks, "전체", null, ETodoTaskListKind.Aggregate));
        }

        return taskLists;
    }

    public async Task<IReadOnlyList<TodoTaskListReference>> GetWritableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        IReadOnlyList<TodoTaskListReference> taskLists = await getTaskListsAsync(userSession, ETodoTaskListAccessMode.Writable, cancellationToken);
        return taskLists;
    }

    public async Task<IReadOnlyList<TodoTaskListReference>> GetWritableTaskListsAsync(UserSession userSession, EResourceOperation operation, CancellationToken cancellationToken)
    {
        PermissionKey permission = ResourceOperationPermission.GetTodoPermission(operation);
        IReadOnlyList<TodoTaskListReference> taskLists = await GetWritableTaskListsAsync(userSession, cancellationToken);
        List<TodoTaskListReference> allowedLists = new List<TodoTaskListReference>();
        foreach (TodoTaskListReference taskList in taskLists)
        {
            if (await canUseOwnedTaskPermissionAsync(userSession, permission, taskList.TeamIdOrNull, cancellationToken))
            {
                allowedLists.Add(taskList);
            }
        }

        return allowedLists;
    }

    public async Task<IReadOnlyList<TodoTaskItem>> GetTasksAsync(
        UserSession userSession,
        TodoTaskListId taskListId,
        CancellationToken cancellationToken
    )
    {
        if (taskListId.IsAggregate)
        {
            return await getAggregateTasksAsync(userSession, cancellationToken);
        }

        TodoTaskListReference taskList = await getAllowedTaskListAsync(userSession, taskListId, ETodoTaskListAccessMode.Readable, cancellationToken);
        GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
        IReadOnlyList<TodoTaskItem> tasks = await getTasksAsync(tasksService, taskList.TaskListId, taskList.DisplayName, cancellationToken);
        return await applyItemPermissionsAsync(userSession, tasks, true, cancellationToken);
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
            fetchTasks.Add(getTasksAsync(tasksService, taskList.TaskListId, taskList.DisplayName, cancellationToken));
        }

        IReadOnlyList<TodoTaskItem>[] taskGroups = await Task.WhenAll(fetchTasks);
        List<TodoTaskItem> mergedTasks = new List<TodoTaskItem>();
        foreach (IReadOnlyList<TodoTaskItem> taskGroup in taskGroups)
        {
            mergedTasks.AddRange(taskGroup);
        }

        List<TodoTaskItem> orderedTasks = mergedTasks.OrderBy(task => task.IsCompleted).ThenBy(task => task.TaskListDisplayName).ThenBy(task => task.Position).ThenBy(task => task.Title).ToList();
        return await applyItemPermissionsAsync(userSession, orderedTasks, true, cancellationToken);
    }

    private async Task<IReadOnlyList<TodoTaskItem>> getTasksAsync(
        GoogleTasksApi tasksService,
        TodoTaskListId taskListId,
        string taskListDisplayName,
        CancellationToken cancellationToken
    )
    {
        List<TodoTaskItem> tasks = new List<TodoTaskItem>();
        string pageToken = string.Empty;
        string requiredTaskListId = taskListId.GetRequiredValue();
        do
        {
            Google.Apis.Tasks.v1.TasksResource.ListRequest request = tasksService.Tasks.List(requiredTaskListId);
            request.ShowCompleted = true;
            request.ShowDeleted = false;
            request.ShowHidden = false;
            request.MaxResults = TASK_PAGE_SIZE;
            request.PageToken = string.IsNullOrWhiteSpace(pageToken) ? null : pageToken;
            request.Fields = "items(" + TASK_FIELDS + "),nextPageToken";
            GoogleTaskList taskList = await request.ExecuteAsync(cancellationToken);
            if (taskList.Items != null)
            {
                foreach (GoogleTask googleTask in taskList.Items)
                {
                    TodoTaskItem taskItem = createTaskItem(taskListId, googleTask);
                    taskItem.TaskListDisplayName = taskListDisplayName;
                    tasks.Add(taskItem);
                }
            }

            pageToken = getTextOrEmpty(taskList.NextPageToken);
        }
        while (string.IsNullOrWhiteSpace(pageToken) == false);

        Dictionary<string, TodoTaskMetadata> metadataByTaskId = await getMetadataByTaskIdAsync(taskListId, tasks, cancellationToken);
        applyMetadata(tasks, metadataByTaskId);
        List<TodoTaskItem> orderedTasks = tasks.OrderBy(task => task.IsCompleted).ThenBy(task => task.Position).ThenBy(task => task.Title).ToList();
        return orderedTasks;
    }
}
