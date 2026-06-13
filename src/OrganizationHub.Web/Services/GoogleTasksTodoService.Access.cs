using System;
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
}
