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
            UserSession teamUserSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);
            UserSession responsibleSession = createPlanningUserSession(EStoredCompatibilityRole.TeamLeader);
            UserSession managementSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(2),
                    Username = Username.CreateOptional("management.staff"),
                    DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });
            UserSession directorSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(3),
                    Username = Username.CreateOptional("director"),
                    DisplayName = DisplayName.CreateOptional("Director"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementDirector,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });

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
            Assert.True(hasTaskList(leaderTaskLists, "management-tasks"));
            Assert.True(hasTaskList(leaderTaskLists, "operation-tasks"));
            Assert.False(hasTaskList(leaderWritableTaskLists, TodoTaskListReference.AGGREGATE_TASK_LIST_ID));
            Assert.True(hasTaskList(leaderWritableTaskLists, "management-tasks"));
            Assert.True(hasTaskList(leaderWritableTaskLists, "responsible-tasks"));
            Assert.True(hasTaskList(leaderWritableTaskLists, "planning-tasks"));
            Assert.True(hasTaskList(leaderWritableTaskLists, "operation-tasks"));
            Assert.True(hasTaskList(managementTaskLists, TodoTaskListReference.AGGREGATE_TASK_LIST_ID));
            Assert.True(hasTaskList(managementTaskLists, "management-tasks"));
            Assert.True(hasTaskList(managementTaskLists, "responsible-tasks"));
            Assert.True(hasTaskList(managementTaskLists, "planning-tasks"));
            Assert.True(hasTaskList(managementTaskLists, "operation-tasks"));
            Assert.True(hasTaskList(managementWritableTaskLists, "management-tasks"));
            Assert.True(hasTaskList(managementWritableTaskLists, "responsible-tasks"));
            Assert.True(hasTaskList(managementWritableTaskLists, "planning-tasks"));
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
            UserSession teamUserSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => todoService.GetTasksAsync(teamUserSession, TodoTaskListId.CreateOptional("operation-tasks"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task GlobalTodoWritePermissionAllowsTeamAndResponsibleTaskListsAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            GoogleTasksTodoService todoService = createTodoService(dbContextFactory);
            UserSession managementSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(2),
                    Username = Username.CreateOptional("management.staff"),
                    DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });
            IReadOnlyList<TodoTaskListReference> writableTaskLists = await todoService.GetWritableTaskListsAsync(managementSession, CancellationToken.None);

            Assert.True(hasTaskList(writableTaskLists, "planning-tasks"));
            Assert.True(hasTaskList(writableTaskLists, "responsible-tasks"));
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
            setting.Id = 1;
            setting.OrganizationTaskListId = "management-tasks";
            setting.ResponsibleTaskListId = "responsible-tasks";
            Team planningTeam = createTeam(1, "Planning", "기획팀", "planning-tasks", 10);
            Team operationTeam = createTeam(2, "Operation", "운영팀", "operation-tasks", 20);
            Team managementTeam = createTeam(3, "Management", "관리팀", string.Empty, 0);
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(managementTeam);
            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(operationTeam);
            seedPermissionUsers(dbContext);
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static void seedPermissionUsers(OrganizationHubDbContext dbContext)
    {
        OrganizationPosition planningMemberPosition = createPosition(1, "기획팀 구성원", 10);
        addPermissionGrant(planningMemberPosition, PermissionKeys.TODOS_READ, EPermissionScopeType.OwnTeam);

        OrganizationPosition planningLeaderPosition = createPosition(1, "기획팀 책임자", 20);
        addPermissionGrant(planningLeaderPosition, PermissionKeys.TODOS_READ, EPermissionScopeType.Global);
        addPermissionGrant(planningLeaderPosition, PermissionKeys.TODOS_CREATE, EPermissionScopeType.Global);

        OrganizationPosition managementStaffPosition = createPosition(3, "관리팀 담당자", 10);
        addPermissionGrant(managementStaffPosition, PermissionKeys.TODOS_READ, EPermissionScopeType.Global);
        addPermissionGrant(managementStaffPosition, PermissionKeys.TODOS_CREATE, EPermissionScopeType.Global);

        OrganizationPosition directorPosition = createPosition(3, "관리 책임자", 20);
        addPermissionGrant(directorPosition, PermissionKeys.TODOS_READ, EPermissionScopeType.Global);
        addPermissionGrant(directorPosition, PermissionKeys.TODOS_CREATE, EPermissionScopeType.Global);

        dbContext.Positions.Add(planningMemberPosition);
        dbContext.Positions.Add(planningLeaderPosition);
        dbContext.Positions.Add(managementStaffPosition);
        dbContext.Positions.Add(directorPosition);
        dbContext.UserAccounts.Add(createUser(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1, planningMemberPosition));
        dbContext.UserAccounts.Add(createUser(11, "planning.leader", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1, planningLeaderPosition));
        dbContext.UserAccounts.Add(createUser(2, "management.staff", "관리팀 담당자", EStoredCompatibilityRole.ManagementStaff, 3, managementStaffPosition));
        dbContext.UserAccounts.Add(createUser(3, "director", "Director", EStoredCompatibilityRole.ManagementDirector, 3, directorPosition));
    }

    private static OrganizationPosition createPosition(int teamId, string name, int sortOrder)
    {
        OrganizationPosition position = new OrganizationPosition();
        position.TeamId = teamId;
        position.Name = name;
        position.Description = string.Empty;
        position.SortOrder = sortOrder;
        position.IsActive = true;
        return position;
    }

    private static void addPermissionGrant(OrganizationPosition position, string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
        permissionGrant.Position = position;
        permissionGrant.PermissionKey = permissionKey;
        permissionGrant.ScopeType = scopeType;
        permissionGrant.IsAllowed = true;
        position.PermissionGrants.Add(permissionGrant);
    }

    private static UserAccount createUser(int id, string username, string displayName, EStoredCompatibilityRole role, int teamId, OrganizationPosition position)
    {
        UserAccount userAccount = new UserAccount();
        userAccount.Id = id;
        userAccount.Username = username;
        userAccount.DisplayName = displayName;
        userAccount.PasswordHash = "test";
        userAccount.StoredCompatibilityRole = role;
        userAccount.TeamId = teamId;
        userAccount.Position = position;
        userAccount.IsActive = true;
        userAccount.CreatedAt = DateTime.UtcNow;
        return userAccount;
    }

    private static Team createTeam(int id, string teamKey, string displayName, string taskListId, int sortOrder)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.TaskListId = taskListId;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private static GoogleTasksTodoService createTodoService(TestDbContextFactory dbContextFactory)
    {
        FakeGoogleApiCredentialFactory credentialFactory = new FakeGoogleApiCredentialFactory();
        GoogleApiOptions options = new GoogleApiOptions();
        options.ApplicationName = "Organization Hub Tests";
        return new GoogleTasksTodoService(dbContextFactory, credentialFactory, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(options), new ChangeVersionService(), new OrganizationPermissionService(dbContextFactory));
    }

    private static async Task<UserSession> seedConfiguredPermissionUserAsync(TestDbContextFactory dbContextFactory, int userId, int teamId, EPermissionScopeType scopeType, IReadOnlyList<string> permissionKeys)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition position = new OrganizationPosition();
            position.TeamId = teamId;
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
            userAccount.Id = userId;
            userAccount.Username = "configured.todo";
            userAccount.DisplayName = "할 일 권한 사용자";
            userAccount.PasswordHash = "test";
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser;
            userAccount.TeamId = teamId;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(userAccount.Id),
                    Username = Username.CreateOptional(userAccount.Username),
                    DisplayName = DisplayName.CreateOptional(userAccount.DisplayName),
                    StoredCompatibilityRole = userAccount.StoredCompatibilityRole,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(teamId),
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });
        }
    }

    private static UserSession createPlanningUserSession(EStoredCompatibilityRole role)
    {
        if (role == EStoredCompatibilityRole.TeamLeader)
        {
            return new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(11),
                    Username = Username.CreateOptional("planning.leader"),
                    DisplayName = DisplayName.CreateOptional("기획 책임자"),
                    StoredCompatibilityRole = role,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });
        }

        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(1),
                Username = Username.CreateOptional("planning.member"),
                DisplayName = DisplayName.CreateOptional("기획팀원"),
                StoredCompatibilityRole = role,
                OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                TeamKey = new OrganizationTeamKey("Planning"),
                TeamDisplayName = DisplayName.CreateOptional("기획팀")
            });
    }

    private static bool hasTaskList(IReadOnlyList<TodoTaskListReference> taskLists, string taskListId)
    {
        TodoTaskListReference? taskListOrNull = taskLists.FirstOrDefault(taskList => taskList.TaskListId.GetRequiredValue() == taskListId);
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
