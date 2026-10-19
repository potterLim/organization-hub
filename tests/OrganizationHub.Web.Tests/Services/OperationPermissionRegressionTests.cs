using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class OperationPermissionRegressionTests
{
    private sealed class CredentialReachedException : Exception
    {
    }

    private sealed class CredentialProbe : IGoogleApiCredentialFactory
    {
        public int CallCount
        {
            get; private set;
        }

        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new CredentialReachedException();
        }
    }

    public static IEnumerable<object[]> GetOperationCases()
    {
        foreach (string granted in new string[] { "create", "update", "delete" })
        {
            foreach (string operation in new string[] { "create", "update", "delete", "complete", "reopen", "move" })
            {
                yield return new object[] { granted, operation };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetOperationCases))]
    public async Task TodoOperationsRequireTheirOwnPermissionBeforeGoogleAsync(string granted, string operation)
    {
        using (TestDbContextFactory factory = createFactory("todos." + granted))
        {
            CredentialProbe credential = new CredentialProbe();
            GoogleTasksTodoService service = new GoogleTasksTodoService(factory, credential, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), new OrganizationPermissionService(factory));
            string required = operation == "complete" || operation == "reopen" || operation == "move" ? "update" : operation;
            if (granted == required)
            {
                await Assert.ThrowsAsync<CredentialReachedException>(() => executeTodoAsync(service, operation));
                Assert.Equal(1, credential.CallCount);
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => executeTodoAsync(service, operation));
                Assert.Equal(0, credential.CallCount);
            }
        }
    }

    [Theory]
    [InlineData("create", "create")]
    [InlineData("create", "update")]
    [InlineData("create", "delete")]
    [InlineData("update", "create")]
    [InlineData("update", "update")]
    [InlineData("update", "delete")]
    [InlineData("delete", "create")]
    [InlineData("delete", "update")]
    [InlineData("delete", "delete")]
    public async Task CalendarOperationsRequireTheirOwnPermissionBeforeGoogleAsync(string granted, string operation)
    {
        using (TestDbContextFactory factory = createFactory("calendar." + granted))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            CredentialProbe credential = new CredentialProbe();
            OrganizationPermissionService organization = new OrganizationPermissionService(factory);
            PermissionService permissions = new PermissionService(factory, new FakeDriveService(), cache, organization);
            GoogleCalendarService service = new GoogleCalendarService(credential, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), permissions);
            if (granted == operation)
            {
                await Assert.ThrowsAsync<CredentialReachedException>(() => executeCalendarAsync(service, operation));
                Assert.Equal(1, credential.CallCount);
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => executeCalendarAsync(service, operation));
                Assert.Equal(0, credential.CallCount);
            }
        }
    }

    internal static TestDbContextFactory createFactory(string permission, TestDbContextFactory? factoryOrNull = null)
    {
        TestDbContextFactory factory = factoryOrNull == null ? new TestDbContextFactory() : factoryOrNull;
        using (OrganizationHubDbContext db = factory.CreateDbContext())
        {
            Team team = new Team();
            team.Id = 1;
            team.TeamKey = "Planning";
            team.DisplayName = "검증팀";
            team.IsActive = true;
            OrganizationPosition position = new OrganizationPosition();
            position.Id = 1;
            position.TeamId = 1;
            position.Name = "검증 직책";
            position.IsActive = true;
            PositionPermissionGrant grant = new PositionPermissionGrant();
            grant.PermissionKey = permission;
            grant.IsAllowed = true;
            grant.ScopeType = EPermissionScopeType.Global;
            position.PermissionGrants.Add(grant);
            UserAccount user = new UserAccount();
            user.Id = 1;
            user.Username = "regression";
            user.DisplayName = "검증 사용자";
            user.TeamId = 1;
            user.PositionId = 1;
            user.IsActive = true;
            GlobalSetting setting = new GlobalSetting();
            setting.OrganizationCalendarId = "calendar";
            setting.OrganizationTaskListId = "tasks";
            db.Teams.Add(team);
            db.Positions.Add(position);
            db.UserAccounts.Add(user);
            db.GlobalSettings.Add(setting);
            db.SaveChanges();
        }

        return factory;
    }

    internal static UserSession createSession()
    {
        return new UserSession(new UserSessionCreateRequest
        {
            UserAccountId = UserAccountId.CreateExisting(1),
            Username = Username.CreateOptional("regression"),
            DisplayName = DisplayName.CreateOptional("검증 사용자"),
            OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
            TeamKey = new OrganizationTeamKey("Planning"),
            TeamDisplayName = DisplayName.CreateOptional("검증팀"),
            StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser
        });
    }

    private static async Task executeTodoAsync(GoogleTasksTodoService service, string operation)
    {
        UserSession session = createSession();
        TodoTaskListId listId = TodoTaskListId.CreateOptional("tasks");
        TodoTaskId taskId = TodoTaskId.CreateOptional("task");
        TodoTaskFormModel form = new TodoTaskFormModel();
        form.Title = "검증";
        form.TaskListId = "tasks";
        form.TaskId = "task";
        switch (operation)
        {
            case "create":
                await service.CreateTaskAsync(session, form, CancellationToken.None);
                break;
            case "update":
                await service.UpdateTaskAsync(session, form, CancellationToken.None);
                break;
            case "delete":
                await service.DeleteTaskAsync(session, listId, taskId, CancellationToken.None);
                break;
            case "complete":
                await service.CompleteTaskAsync(session, listId, taskId, CancellationToken.None);
                break;
            case "reopen":
                await service.ReopenTaskAsync(session, listId, taskId, CancellationToken.None);
                break;
            case "move":
                await service.MoveTaskAsync(session, listId, taskId, TodoTaskId.Empty, CancellationToken.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static async Task executeCalendarAsync(GoogleCalendarService service, string operation)
    {
        UserSession session = createSession();
        CalendarEventFormModel form = new CalendarEventFormModel();
        form.CalendarId = "calendar";
        form.Id = "event";
        form.Summary = "검증";
        form.StartLocal = new DateTime(2026, 10, 19, 9, 0, 0);
        form.EndLocal = form.StartLocal.AddHours(1);
        switch (operation)
        {
            case "create":
                await service.CreateEventAsync(session, form, CalendarName.CreateOptional("검증"), CancellationToken.None);
                break;
            case "update":
                await service.UpdateEventAsync(session, form, CalendarName.CreateOptional("검증"), CancellationToken.None);
                break;
            case "delete":
                await service.DeleteEventAsync(session, CalendarId.CreateOptional("calendar"), CalendarEventId.CreateOptional("event"), CancellationToken.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
