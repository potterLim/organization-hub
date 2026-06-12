using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TodoServiceTests
{
    [Fact]
    public async Task TodoPolicySeparatesTeamMemberLeaderAndManagementAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            GoogleTasksTodoService todoService = createTodoService(dbContextFactory);
            UserSession teamUserSession = createPlanningUserSession(EUserRole.TeamUser);
            UserSession responsibleSession = createPlanningUserSession(EUserRole.TeamLeader);
            UserSession managementSession = new UserSession(2, "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, 3, "Management", "관리팀");
            UserSession directorSession = new UserSession(3, "director", "Director", EUserRole.ManagementDirector, 3, "Management", "관리팀");

            IReadOnlyList<TodoTaskListReference> teamUserTaskLists = await todoService.GetReadableTaskListsAsync(teamUserSession, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> leaderTaskLists = await todoService.GetReadableTaskListsAsync(responsibleSession, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> leaderWritableTaskLists = await todoService.GetWritableTaskListsAsync(responsibleSession, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> managementTaskLists = await todoService.GetReadableTaskListsAsync(managementSession, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> managementWritableTaskLists = await todoService.GetWritableTaskListsAsync(managementSession, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> directorWritableTaskLists = await todoService.GetWritableTaskListsAsync(directorSession, CancellationToken.None);

            Assert.True(hasTaskList(teamUserTaskLists, TodoTaskListReference.AGGREGATE_TASK_LIST_ID));
            Assert.True(hasTaskList(teamUserTaskLists, "planning-tasks"));
            Assert.False(hasTaskList(teamUserTaskLists, "responsible-tasks"));
            Assert.False(hasTaskList(teamUserTaskLists, "operation-tasks"));
            Assert.True(hasTaskList(leaderTaskLists, TodoTaskListReference.AGGREGATE_TASK_LIST_ID));
            Assert.True(hasTaskList(leaderTaskLists, "responsible-tasks"));
            Assert.True(hasTaskList(leaderTaskLists, "planning-tasks"));
            Assert.False(hasTaskList(leaderTaskLists, "management-tasks"));
            Assert.False(hasTaskList(leaderTaskLists, "operation-tasks"));
            Assert.False(hasTaskList(leaderWritableTaskLists, TodoTaskListReference.AGGREGATE_TASK_LIST_ID));
            Assert.True(hasTaskList(managementTaskLists, TodoTaskListReference.AGGREGATE_TASK_LIST_ID));
            Assert.True(hasTaskList(managementTaskLists, "management-tasks"));
            Assert.True(hasTaskList(managementTaskLists, "responsible-tasks"));
            Assert.True(hasTaskList(managementTaskLists, "planning-tasks"));
            Assert.True(hasTaskList(managementTaskLists, "operation-tasks"));
            Assert.True(hasTaskList(managementWritableTaskLists, "management-tasks"));
            Assert.False(hasTaskList(managementWritableTaskLists, "responsible-tasks"));
            Assert.False(hasTaskList(managementWritableTaskLists, "planning-tasks"));
            Assert.True(hasTaskList(directorWritableTaskLists, "responsible-tasks"));
            Assert.True(hasTaskList(directorWritableTaskLists, "planning-tasks"));
            Assert.True(hasTaskList(directorWritableTaskLists, "operation-tasks"));
        }
    }

    [Fact]
    public async Task DirectTaskListRequestsRespectTodoPolicyBeforeGoogleCallAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            GoogleTasksTodoService todoService = createTodoService(dbContextFactory);
            UserSession teamUserSession = createPlanningUserSession(EUserRole.TeamUser);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => todoService.GetTasksAsync(teamUserSession, "operation-tasks", CancellationToken.None));
        }
    }

    [Fact]
    public async Task GeneralManagementCannotWriteTeamOrResponsibleTaskListsAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            GoogleTasksTodoService todoService = createTodoService(dbContextFactory);
            UserSession managementSession = new UserSession(2, "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, 3, "Management", "관리팀");
            TodoTaskFormModel teamTaskForm = new TodoTaskFormModel();
            teamTaskForm.TaskListID = "planning-tasks";
            teamTaskForm.Title = "팀 업무";
            TodoTaskFormModel leaderTaskForm = new TodoTaskFormModel();
            leaderTaskForm.TaskListID = "responsible-tasks";
            leaderTaskForm.Title = "책임자 업무";

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => todoService.CreateTaskAsync(managementSession, teamTaskForm, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => todoService.CreateTaskAsync(managementSession, leaderTaskForm, CancellationToken.None));
        }
    }

    [Fact]
    public async Task ConfiguredPositionGrantsOwnTeamTodoReadAndWriteAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            UserSession userSession = await seedConfiguredPermissionUserAsync(dbContextFactory, 41, 1, EPermissionScopeType.OwnTeam, new List<string> { PermissionKeys.TODOS_READ, PermissionKeys.TODOS_CREATE });
            GoogleTasksTodoService todoService = createTodoService(dbContextFactory);

            IReadOnlyList<TodoTaskListReference> readableTaskLists = await todoService.GetReadableTaskListsAsync(userSession, CancellationToken.None);
            IReadOnlyList<TodoTaskListReference> writableTaskLists = await todoService.GetWritableTaskListsAsync(userSession, CancellationToken.None);

            Assert.True(hasTaskList(readableTaskLists, TodoTaskListReference.AGGREGATE_TASK_LIST_ID));
            Assert.True(hasTaskList(readableTaskLists, "planning-tasks"));
            Assert.False(hasTaskList(readableTaskLists, "operation-tasks"));
            Assert.False(hasTaskList(readableTaskLists, "responsible-tasks"));
            Assert.True(hasTaskList(writableTaskLists, "planning-tasks"));
            Assert.False(hasTaskList(writableTaskLists, "operation-tasks"));
        }
    }

    private static TestDbContextFactory createConfiguredDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.ID = 1;
            setting.OrganizationTaskListID = "management-tasks";
            setting.ResponsibleTaskListID = "responsible-tasks";
            Team planningTeam = createTeam(1, "Planning", "기획팀", "planning-tasks", 10);
            Team operationTeam = createTeam(2, "Operation", "운영팀", "operation-tasks", 20);
            Team managementTeam = createTeam(3, "Management", "관리팀", string.Empty, 0);
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(managementTeam);
            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(operationTeam);
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static Team createTeam(int id, string teamKey, string displayName, string taskListID, int sortOrder)
    {
        Team team = new Team();
        team.ID = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.TaskListID = taskListID;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private static GoogleTasksTodoService createTodoService(TestDbContextFactory dbContextFactory)
    {
        FakeGoogleApiCredentialFactory credentialFactory = new FakeGoogleApiCredentialFactory();
        GoogleApiOptions options = new GoogleApiOptions();
        options.ApplicationName = "Organization Hub Tests";
        return new GoogleTasksTodoService(dbContextFactory, credentialFactory, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(options), new ChangeVersionService());
    }

    private static async Task<UserSession> seedConfiguredPermissionUserAsync(TestDbContextFactory dbContextFactory, int userID, int teamID, EPermissionScopeType scopeType, IReadOnlyList<string> permissionKeys)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition position = new OrganizationPosition();
            position.TeamID = teamID;
            position.Name = "할 일 담당";
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            dbContext.Positions.Add(position);

            foreach (string permissionKey in permissionKeys)
            {
                PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
                permissionGrant.Position = position;
                permissionGrant.PermissionKey = permissionKey;
                permissionGrant.ScopeType = scopeType;
                permissionGrant.IsAllowed = true;
                dbContext.PositionPermissionGrants.Add(permissionGrant);
            }

            UserAccount userAccount = new UserAccount();
            userAccount.ID = userID;
            userAccount.Username = "configured.todo";
            userAccount.DisplayName = "할 일 권한 사용자";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.TeamUser;
            userAccount.TeamID = teamID;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new UserSession(userAccount.ID, userAccount.Username, userAccount.DisplayName, userAccount.Role, teamID, "Planning", "기획팀");
        }
    }

    private static UserSession createPlanningUserSession(EUserRole role)
    {
        return new UserSession(1, "planning", "Planning User", role, 1, "Planning", "기획팀");
    }

    private static bool hasTaskList(IReadOnlyList<TodoTaskListReference> taskLists, string taskListID)
    {
        TodoTaskListReference? taskListOrNull = taskLists.FirstOrDefault(taskList => taskList.TaskListID == taskListID);
        return taskListOrNull != null;
    }

    private sealed class FakeGoogleApiCredentialFactory : IGoogleApiCredentialFactory
    {
        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Google API should not be called by permission-only todo tests.");
        }
    }
}
