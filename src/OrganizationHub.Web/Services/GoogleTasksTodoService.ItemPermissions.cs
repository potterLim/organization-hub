using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    private async Task<ResourceOperationAccess> getTaskListOperationAccessAsync(UserSession userSession, TodoTaskListId taskListId, EResourceOperation operation, CancellationToken cancellationToken)
    {
        IReadOnlyList<TodoTaskListReference> lists = operation == EResourceOperation.Read
            ? await GetReadableTaskListsAsync(userSession, cancellationToken)
            : await GetWritableTaskListsAsync(userSession, operation, cancellationToken);
        List<OrganizationTeamId?> targetTeamIds = lists.Where(list => list.TaskListId == taskListId).Select(list => list.TeamIdOrNull).ToList();
        return await ResourceOperationAccessUtility.ResolveAsync(mOrganizationPermissionService, userSession, ResourceOperationPermission.GetTodoPermission(operation), targetTeamIds, cancellationToken);
    }

    private async Task<IReadOnlyList<TodoTaskItem>> applyItemPermissionsAsync(UserSession userSession, IReadOnlyList<TodoTaskItem> tasks, bool shouldFilterReadAccess, CancellationToken cancellationToken)
    {
        HashSet<TodoTaskItem> visibleTasks = new HashSet<TodoTaskItem>();
        foreach (IGrouping<string, TodoTaskItem> group in tasks.GroupBy(item => item.TaskListId))
        {
            TodoTaskListId listId = TodoTaskListId.CreateOptional(group.Key);
            ResourceOperationAccess update = await getTaskListOperationAccessAsync(userSession, listId, EResourceOperation.Update, cancellationToken);
            ResourceOperationAccess delete = await getTaskListOperationAccessAsync(userSession, listId, EResourceOperation.Delete, cancellationToken);
            ResourceOperationAccess read = shouldFilterReadAccess
                ? await getTaskListOperationAccessAsync(userSession, listId, EResourceOperation.Read, cancellationToken)
                : new ResourceOperationAccess(true, true);
            foreach (TodoTaskItem item in group)
            {
                item.CanEdit = update.CanUse(userSession.UserAccountId, item.CreatedByUserIdOrNull);
                item.CanDelete = delete.CanUse(userSession.UserAccountId, item.CreatedByUserIdOrNull);
                if (read.CanUse(userSession.UserAccountId, item.CreatedByUserIdOrNull))
                {
                    visibleTasks.Add(item);
                }
            }
        }

        return tasks.Where(visibleTasks.Contains).ToList();
    }

    private async Task ensureCanModifyTaskAsync(UserSession userSession, TodoTaskListId taskListId, TodoTaskId taskId, EResourceOperation operation, CancellationToken cancellationToken)
    {
        ResourceOperationAccess access = await getTaskListOperationAccessAsync(userSession, taskListId, operation, cancellationToken);
        string requiredListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        int? ownerUserIdOrNull;
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ownerUserIdOrNull = await db.TodoTaskMetadata.Where(item => item.TaskListId == requiredListId && item.TaskId == requiredTaskId).Select(item => (int?)item.CreatedByUserId).SingleOrDefaultAsync(cancellationToken);
        }

        if (access.CanUse(userSession.UserAccountId, ownerUserIdOrNull) == false)
        {
            throw new UnauthorizedAccessException("이 할 일을 변경할 권한이 없습니다.");
        }
    }

    private async Task<bool> canUseOwnedTaskPermissionAsync(UserSession userSession, PermissionKey permissionKey, OrganizationTeamId? targetTeamIdOrNull, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await mOrganizationPermissionService.DecidePermissionAsync(userSession, permissionKey, targetTeamIdOrNull, userSession.UserAccountId, cancellationToken);
        return decision.IsAllowed;
    }
}
