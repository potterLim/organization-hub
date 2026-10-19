using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    private async Task<IReadOnlyList<TodoTaskListReference>> getTaskListsAsync(
        UserSession userSession,
        ETodoTaskListAccessMode accessMode,
        CancellationToken cancellationToken)
    {
        List<TodoTaskListReference> taskLists = new List<TodoTaskListReference>();
        TodoTaskListAccessContext accessContext = new TodoTaskListAccessContext(userSession, accessMode);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                await addGlobalTaskListsAsync(taskLists, settingOrNull, accessContext, cancellationToken);
            }

            List<Team> teams = await dbContext.Teams
                .Where(team => team.IsActive && team.TaskListId != null && team.TaskListId != string.Empty)
                .OrderBy(team => team.SortOrder)
                .ThenBy(team => team.DisplayName)
                .ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                TodoTaskListPermissionRequest permissionRequest = new TodoTaskListPermissionRequest(accessContext, OrganizationTeamId.CreateExisting(team.Id));
                bool canUseTaskList = await canUseTaskListAsync(permissionRequest, cancellationToken);
                if (canUseTaskList)
                {
                    addTaskListIfConfigured(taskLists, team.TaskListId, team.DisplayName, OrganizationTeamId.CreateExisting(team.Id));
                }
            }
        }

        return taskLists;
    }

    private async Task addGlobalTaskListsAsync(
        List<TodoTaskListReference> taskLists,
        GlobalSetting setting,
        TodoTaskListAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        TodoTaskListPermissionRequest managementRequest = new TodoTaskListPermissionRequest(accessContext, null);
        bool canUseManagementTaskList = await canUseTaskListAsync(managementRequest, cancellationToken);
        if (canUseManagementTaskList)
        {
            addTaskListIfConfigured(taskLists, setting.OrganizationTaskListId, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, null);
        }

        TodoTaskListPermissionRequest responsibleRequest = new TodoTaskListPermissionRequest(accessContext, null);
        bool canUseResponsibleTaskList = await canUseTaskListAsync(responsibleRequest, cancellationToken);
        if (canUseResponsibleTaskList)
        {
            addTaskListIfConfigured(taskLists, setting.ResponsibleTaskListId, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, null);
        }
    }

    private async Task<bool> canUseTaskListAsync(TodoTaskListPermissionRequest request, CancellationToken cancellationToken)
    {
        if (request.AccessContext.AccessMode != ETodoTaskListAccessMode.Writable)
        {
            return await canUseOwnedTaskPermissionAsync(
                request.AccessContext.UserSession,
                new PermissionKey(PermissionKeys.TODOS_READ),
                request.TargetTeamIdOrNull,
                cancellationToken);
        }

        bool canCreate = await canUseOwnedTaskPermissionAsync(
            request.AccessContext.UserSession,
            new PermissionKey(PermissionKeys.TODOS_CREATE),
            request.TargetTeamIdOrNull,
            cancellationToken);
        if (canCreate)
        {
            return true;
        }

        bool canUpdate = await canUseOwnedTaskPermissionAsync(
            request.AccessContext.UserSession,
            new PermissionKey(PermissionKeys.TODOS_UPDATE),
            request.TargetTeamIdOrNull,
            cancellationToken);
        if (canUpdate)
        {
            return true;
        }

        return await canUseOwnedTaskPermissionAsync(
            request.AccessContext.UserSession,
            new PermissionKey(PermissionKeys.TODOS_DELETE),
            request.TargetTeamIdOrNull,
            cancellationToken);
    }

    private void addTaskListIfConfigured(
        List<TodoTaskListReference> taskLists,
        string? taskListIdOrNull,
        string displayName,
        OrganizationTeamId? teamIdOrNull)
    {
        if (string.IsNullOrWhiteSpace(taskListIdOrNull))
        {
            return;
        }

        TodoTaskListId taskListId = TodoTaskListId.CreateOptional(taskListIdOrNull);
        bool isAlreadyAdded = taskLists.Any(taskList => taskList.TaskListId == taskListId);
        if (isAlreadyAdded)
        {
            return;
        }

        taskLists.Add(new TodoTaskListReference(taskListId, displayName, teamIdOrNull));
    }

    private sealed class TodoTaskListAccessContext
    {
        public TodoTaskListAccessContext(UserSession userSession, ETodoTaskListAccessMode accessMode)
        {
            UserSession = userSession;
            AccessMode = accessMode;
        }

        public UserSession UserSession
        {
            get; private init;
        }

        public ETodoTaskListAccessMode AccessMode
        {
            get; private init;
        }
    }

    private sealed class TodoTaskListPermissionRequest
    {
        public TodoTaskListPermissionRequest(
            TodoTaskListAccessContext accessContext,
            OrganizationTeamId? targetTeamIdOrNull)
        {
            AccessContext = accessContext;
            TargetTeamIdOrNull = targetTeamIdOrNull;
        }

        public TodoTaskListAccessContext AccessContext
        {
            get; private init;
        }

        public OrganizationTeamId? TargetTeamIdOrNull
        {
            get; private init;
        }
    }
}
