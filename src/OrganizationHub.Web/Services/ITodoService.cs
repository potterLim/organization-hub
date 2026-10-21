using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface ITodoService
{
    Task<IReadOnlyList<TodoTaskListReference>> GetReadableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoTaskListReference>> GetWritableTaskListsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoTaskListReference>> GetWritableTaskListsAsync(UserSession userSession, EResourceOperation operation, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoTaskItem>> GetTasksAsync(UserSession userSession, TodoTaskListId taskListId, CancellationToken cancellationToken);
    Task<TodoTaskItem> CreateTaskAsync(UserSession userSession, TodoTaskFormModel formModel, CancellationToken cancellationToken);
    Task<TodoTaskItem> UpdateTaskAsync(UserSession userSession, TodoTaskFormModel formModel, CancellationToken cancellationToken);
    Task<TodoTaskItem> CompleteTaskAsync(UserSession userSession, TodoTaskListId taskListId, TodoTaskId taskId, CancellationToken cancellationToken);
    Task<TodoTaskItem> ReopenTaskAsync(UserSession userSession, TodoTaskListId taskListId, TodoTaskId taskId, CancellationToken cancellationToken);
    Task DeleteTaskAsync(UserSession userSession, TodoTaskListId taskListId, TodoTaskId taskId, string expectedETag, CancellationToken cancellationToken);
    Task<TodoTaskItem> MoveTaskAsync(UserSession userSession, TodoTaskListId taskListId, TodoTaskId taskId, TodoTaskId previousTaskId, CancellationToken cancellationToken);
}
