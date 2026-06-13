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
}
