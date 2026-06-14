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
        EPermissionConfigurationMode configurationMode = await getTaskPermissionConfigurationModeAsync(userSession, cancellationToken);
        TodoTaskListAccessContext accessContext = new TodoTaskListAccessContext(userSession, accessMode, configurationMode);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                await addGlobalTaskListsAsync(taskLists, settingOrNull, accessContext, cancellationToken);
            }

            IQueryable<Team> teamQuery = dbContext.Teams
                .Where(team => team.IsActive && team.TaskListId != null && team.TaskListId != string.Empty)
                .OrderBy(team => team.SortOrder)
                .ThenBy(team => team.DisplayName);
            bool shouldLimitWritableFallbackToOwnTeam = configurationMode == EPermissionConfigurationMode.FallbackPolicy &&
                accessMode == ETodoTaskListAccessMode.Writable &&
                UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role) == false;
            if (shouldLimitWritableFallbackToOwnTeam)
            {
                if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
                {
                    return taskLists;
                }

                if (userSession.HasTeam == false)
                {
                    return taskLists;
                }

                int teamId = userSession.GetRequiredTeamIdValue();
                teamQuery = teamQuery.Where(team => team.Id == teamId);
            }
            else if (configurationMode == EPermissionConfigurationMode.FallbackPolicy && userSession.IsManagementRole() == false)
            {
                if (userSession.HasTeam == false)
                {
                    return taskLists;
                }

                int teamId = userSession.GetRequiredTeamIdValue();
                teamQuery = teamQuery.Where(team => team.Id == teamId);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                EPermissionFallback teamTaskListFallback = getTeamTaskListFallback(userSession, team, accessMode);
                TodoTaskListPermissionRequest permissionRequest = new TodoTaskListPermissionRequest(accessContext, OrganizationTeamId.CreateExisting(team.Id), teamTaskListFallback);
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
        EPermissionFallback managementFallback = getManagementTaskListFallback(accessContext.UserSession, accessContext.AccessMode);
        TodoTaskListPermissionRequest managementRequest = new TodoTaskListPermissionRequest(accessContext, null, managementFallback);
        bool canUseManagementTaskList = await canUseTaskListAsync(managementRequest, cancellationToken);
        if (canUseManagementTaskList)
        {
            addTaskListIfConfigured(taskLists, setting.OrganizationTaskListId, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, null);
        }

        EPermissionFallback responsibleFallback = getResponsibleTaskListFallback(accessContext.UserSession, accessContext.AccessMode);
        TodoTaskListPermissionRequest responsibleRequest = new TodoTaskListPermissionRequest(accessContext, null, responsibleFallback);
        bool canUseResponsibleTaskList = await canUseTaskListAsync(responsibleRequest, cancellationToken);
        if (canUseResponsibleTaskList)
        {
            addTaskListIfConfigured(taskLists, setting.ResponsibleTaskListId, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, null);
        }
    }

    private async Task<bool> canUseTaskListAsync(TodoTaskListPermissionRequest request, CancellationToken cancellationToken)
    {
        if (request.AccessContext.ConfigurationMode == EPermissionConfigurationMode.FallbackPolicy)
        {
            return PermissionFallbackUtility.IsAllowed(request.FallbackPermission);
        }

        if (request.AccessContext.AccessMode != ETodoTaskListAccessMode.Writable)
        {
            return await mOrganizationPermissionService.HasPermissionAsync(request.AccessContext.UserSession, new PermissionKey(PermissionKeys.TODOS_READ), request.TargetTeamIdOrNull, cancellationToken);
        }

        bool canCreate = await mOrganizationPermissionService.HasPermissionAsync(
            request.AccessContext.UserSession,
            new PermissionKey(PermissionKeys.TODOS_CREATE),
            request.TargetTeamIdOrNull,
            cancellationToken);
        if (canCreate)
        {
            return true;
        }

        bool canUpdate = await mOrganizationPermissionService.HasPermissionAsync(
            request.AccessContext.UserSession,
            new PermissionKey(PermissionKeys.TODOS_UPDATE),
            request.TargetTeamIdOrNull,
            cancellationToken);
        if (canUpdate)
        {
            return true;
        }

        return await mOrganizationPermissionService.HasPermissionAsync(request.AccessContext.UserSession, new PermissionKey(PermissionKeys.TODOS_DELETE), request.TargetTeamIdOrNull, cancellationToken);
    }

    private EPermissionFallback getManagementTaskListFallback(UserSession userSession, ETodoTaskListAccessMode accessMode)
    {
        if (accessMode == ETodoTaskListAccessMode.Writable)
        {
            return PermissionFallbackUtility.FromDecision(UserPermissionPolicy.CanWriteManagementWork(userSession.Role));
        }

        return PermissionFallbackUtility.FromDecision(userSession.IsManagementRole());
    }

    private EPermissionFallback getResponsibleTaskListFallback(UserSession userSession, ETodoTaskListAccessMode accessMode)
    {
        if (accessMode == ETodoTaskListAccessMode.Writable)
        {
            return PermissionFallbackUtility.FromDecision(UserPermissionPolicy.CanWriteResponsibleWork(userSession.Role));
        }

        return PermissionFallbackUtility.FromDecision(userSession.IsManagementRole() || userSession.Role == EUserRole.TeamLeader);
    }

    private EPermissionFallback getTeamTaskListFallback(UserSession userSession, Team team, ETodoTaskListAccessMode accessMode)
    {
        if (accessMode == ETodoTaskListAccessMode.Writable)
        {
            if (UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role))
            {
                return EPermissionFallback.Allow;
            }

            if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
            {
                return EPermissionFallback.Deny;
            }
        }
        else if (userSession.IsManagementRole())
        {
            return EPermissionFallback.Allow;
        }

        if (userSession.HasTeam == false)
        {
            return EPermissionFallback.Deny;
        }

        return PermissionFallbackUtility.FromDecision(userSession.GetRequiredTeamIdValue() == team.Id);
    }

    private async Task<EPermissionConfigurationMode> getTaskPermissionConfigurationModeAsync(
        UserSession userSession,
        CancellationToken cancellationToken
    )
    {
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        if (usesConfiguredPermissions)
        {
            return EPermissionConfigurationMode.ConfiguredPermissions;
        }

        return EPermissionConfigurationMode.FallbackPolicy;
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
        bool alreadyAdded = taskLists.Any(taskList => taskList.TaskListId == taskListId);
        if (alreadyAdded)
        {
            return;
        }

        taskLists.Add(new TodoTaskListReference(taskListId, displayName, teamIdOrNull));
    }

    private sealed class TodoTaskListAccessContext
    {
        public TodoTaskListAccessContext(UserSession userSession, ETodoTaskListAccessMode accessMode, EPermissionConfigurationMode configurationMode)
        {
            UserSession = userSession;
            AccessMode = accessMode;
            ConfigurationMode = configurationMode;
        }

        public UserSession UserSession
        {
            get; private init;
        }

        public ETodoTaskListAccessMode AccessMode
        {
            get; private init;
        }

        public EPermissionConfigurationMode ConfigurationMode
        {
            get; private init;
        }
    }

    private sealed class TodoTaskListPermissionRequest
    {
        public TodoTaskListPermissionRequest(
            TodoTaskListAccessContext accessContext,
            OrganizationTeamId? targetTeamIdOrNull,
            EPermissionFallback fallbackPermission
        )
        {
            AccessContext = accessContext;
            TargetTeamIdOrNull = targetTeamIdOrNull;
            FallbackPermission = fallbackPermission;
        }

        public TodoTaskListAccessContext AccessContext
        {
            get; private init;
        }

        public OrganizationTeamId? TargetTeamIdOrNull
        {
            get; private init;
        }

        public EPermissionFallback FallbackPermission
        {
            get; private init;
        }
    }
}
