using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface ITodoService
{
    Task<IReadOnlyList<TodoTaskListReference>> GetReadableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoTaskListReference>> GetWritableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoTaskItem>> GetTasksAsync(UserSession userSession, string taskListID, CancellationToken cancellationToken);
    Task<TodoTaskItem> CreateTaskAsync(UserSession userSession, TodoTaskFormModel formModel, CancellationToken cancellationToken);
    Task<TodoTaskItem> UpdateTaskAsync(UserSession userSession, TodoTaskFormModel formModel, CancellationToken cancellationToken);
    Task<TodoTaskItem> CompleteTaskAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken);
    Task<TodoTaskItem> ReopenTaskAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken);
    Task DeleteTaskAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken);
    Task<TodoTaskItem> MoveTaskAsync(UserSession userSession, string taskListID, string taskID, string previousTaskID, CancellationToken cancellationToken);
}
