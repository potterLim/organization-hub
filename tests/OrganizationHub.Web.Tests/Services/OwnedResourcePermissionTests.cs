using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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

public sealed class OwnedResourcePermissionTests
{
    private sealed class CredentialReachedException : Exception
    {
    }

    private sealed class CredentialProbe : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        public bool ShouldReturnItems
        {
            get; set;
        }

        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
        {
            if (ShouldReturnItems == false)
            {
                throw new CredentialReachedException();
            }

            return Task.FromResult<IConfigurableHttpClientInitializer>(this);
        }

        public void Initialize(ConfigurableHttpClient httpClient)
        {
            httpClient.MessageHandler.InnerHandler = this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string json = "{\"items\":[";
            List<string> items = new List<string>();
            foreach (string id in new string[] { "owned", "other", "unknown" })
            {
                items.Add("{\"id\":\"" + id + "\",\"summary\":\"" + id + "\",\"title\":\"" + id + "\",\"status\":\"needsAction\",\"start\":{\"date\":\"2026-10-19\"},\"end\":{\"date\":\"2026-10-20\"}}");
            }

            json += string.Join(",", items) + "]}";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    public static IEnumerable<object[]> GetOwnedOperations()
    {
        foreach (string operation in new string[] { "update", "delete", "complete", "reopen", "move" })
        {
            foreach (int owner in new int[] { 1, 2, 0 })
            {
                yield return new object[] { operation, owner };
            }
        }
    }

    [Theory]
    [InlineData("update", 1)]
    [InlineData("update", 2)]
    [InlineData("update", 0)]
    [InlineData("delete", 1)]
    [InlineData("delete", 2)]
    [InlineData("delete", 0)]
    public async Task CalendarOwnCreatedPermissionRequiresRecordedOwnershipBeforeGoogleAsync(string operation, int owner)
    {
        using (TestDbContextFactory factory = createFactory("calendar." + operation, owner))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (CredentialProbe credential = new CredentialProbe())
        {
            GoogleCalendarService service = createCalendarService(factory, cache, credential);
            if (owner == 1)
            {
                await Assert.ThrowsAsync<CredentialReachedException>(() => executeCalendarAsync(service, operation));
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => executeCalendarAsync(service, operation));
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetOwnedOperations))]
    public async Task TodoOwnCreatedPermissionRequiresRecordedOwnershipBeforeGoogleAsync(string operation, int owner)
    {
        string permissionOperation = operation == "delete" ? "delete" : "update";
        using (TestDbContextFactory factory = createFactory("todos." + permissionOperation, owner))
        using (CredentialProbe credential = new CredentialProbe())
        {
            GoogleTasksTodoService service = createTodoService(factory, credential);
            if (owner == 1)
            {
                await Assert.ThrowsAsync<CredentialReachedException>(() => executeTodoAsync(service, operation));
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => executeTodoAsync(service, operation));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnCreatedReadScopeFiltersOtherAndUnknownOwnersAsync(bool isCalendar)
    {
        using (TestDbContextFactory factory = createFactory(isCalendar ? PermissionKeys.CALENDAR_READ : PermissionKeys.TODOS_READ, 1))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (CredentialProbe credential = new CredentialProbe())
        {
            credential.ShouldReturnItems = true;
            if (isCalendar)
            {
                IReadOnlyList<CalendarEventItem> events = await createCalendarService(factory, cache, credential).GetAggregateEventsAsync(OperationPermissionRegressionTests.createSession(), new CalendarEventDateRange(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)), CancellationToken.None);
                Assert.Equal("owned", Assert.Single(events).Id);
            }
            else
            {
                IReadOnlyList<TodoTaskItem> tasks = await createTodoService(factory, credential).GetTasksAsync(OperationPermissionRegressionTests.createSession(), TodoTaskListId.AllTasks, CancellationToken.None);
                Assert.Equal("owned", Assert.Single(tasks).Id);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadModelsExposeOnlyOwnedActionsAndReevaluateRevokedGrantsAsync(bool isCalendar)
    {
        string updatePermission = isCalendar ? PermissionKeys.CALENDAR_UPDATE : PermissionKeys.TODOS_UPDATE;
        using (TestDbContextFactory factory = createFactory(updatePermission, 1))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (CredentialProbe credential = new CredentialProbe())
        {
            credential.ShouldReturnItems = true;
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                PositionPermissionGrant read = new PositionPermissionGrant();
                read.PositionId = 1;
                read.PermissionKey = isCalendar ? PermissionKeys.CALENDAR_READ : PermissionKeys.TODOS_READ;
                read.ScopeType = EPermissionScopeType.Global;
                read.IsAllowed = true;
                db.PositionPermissionGrants.Add(read);
                await db.SaveChangesAsync();
            }

            UserSession session = OperationPermissionRegressionTests.createSession();
            GoogleCalendarService calendar = createCalendarService(factory, cache, credential);
            GoogleTasksTodoService todos = createTodoService(factory, credential);
            if (isCalendar)
            {
                IReadOnlyList<CalendarEventItem> events = await calendar.GetAggregateEventsAsync(session, new CalendarEventDateRange(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)), CancellationToken.None);
                Assert.True(events.Single(item => item.Id == "owned").CanEdit);
                Assert.All(events.Where(item => item.Id != "owned"), item => Assert.False(item.CanEdit));
                Assert.All(events, item => Assert.False(item.CanDelete));
            }
            else
            {
                IReadOnlyList<TodoTaskItem> tasks = await todos.GetTasksAsync(session, TodoTaskListId.AllTasks, CancellationToken.None);
                Assert.True(tasks.Single(item => item.Id == "owned").CanEdit);
                Assert.All(tasks.Where(item => item.Id != "owned"), item => Assert.False(item.CanEdit));
                Assert.All(tasks, item => Assert.False(item.CanDelete));
            }

            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                db.PositionPermissionGrants.Single(grant => grant.PermissionKey == updatePermission).IsAllowed = false;
                await db.SaveChangesAsync();
            }

            if (isCalendar)
            {
                IReadOnlyList<CalendarEventItem> cachedEvents = await calendar.GetAggregateEventsAsync(session, new CalendarEventDateRange(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)), CancellationToken.None);
                Assert.All(cachedEvents, item => Assert.False(item.CanEdit));
            }
            else
            {
                IReadOnlyList<TodoTaskItem> tasks = await todos.GetTasksAsync(session, TodoTaskListId.AllTasks, CancellationToken.None);
                Assert.All(tasks, item => Assert.False(item.CanEdit));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnCreatedReadersCanOpenTheirFilteredResourcePageAsync(bool isCalendar)
    {
        string permission = isCalendar ? PermissionKeys.CALENDAR_READ : PermissionKeys.TODOS_READ;
        using (TestDbContextFactory factory = createFactory(permission, 1))
        {
            string path = isCalendar ? "/calendar" : "/todos";
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                NavigationMenuSection section = new NavigationMenuSection();
                section.SectionKey = "operations";
                section.DisplayName = "운영";
                NavigationMenuItem menu = new NavigationMenuItem();
                menu.MenuKey = isCalendar ? NavigationMenuKey.CALENDAR : NavigationMenuKey.TODOS;
                menu.DisplayName = "본인 항목";
                menu.Href = path;
                menu.IsActive = true;
                menu.IsVisibleToAllTeams = true;
                menu.AccessLevel = EMenuAccessLevel.Authenticated;
                menu.RequiredPermissionKeys = permission;
                menu.Section = section;
                db.NavigationMenuItems.Add(menu);
                await db.SaveChangesAsync();
            }

            NavigationMenuService navigation = new NavigationMenuService(factory, new OrganizationPermissionService(factory));
            Assert.True(await navigation.CanAccessPathAsync(OperationPermissionRegressionTests.createSession(), NavigationPath.CreateOptional(path), CancellationToken.None));
        }
    }

    private static TestDbContextFactory createFactory(string permission, int owner)
    {
        TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(permission);
        using (OrganizationHubDbContext db = factory.CreateDbContext())
        {
            db.PositionPermissionGrants.Single().ScopeType = EPermissionScopeType.OwnCreated;
            if (owner > 0)
            {
                addMetadata(db, "event", "task", owner);
            }

            addMetadata(db, "owned", "owned", 1);
            addMetadata(db, "other", "other", 2);
            db.SaveChanges();
        }

        return factory;
    }

    private static void addMetadata(OrganizationHubDbContext db, string eventId, string taskId, int owner)
    {
        CalendarEventMetadata calendar = new CalendarEventMetadata();
        calendar.CalendarId = "calendar";
        calendar.EventId = eventId;
        calendar.CreatedByUserId = owner;
        TodoTaskMetadata task = new TodoTaskMetadata();
        task.TaskListId = "tasks";
        task.TaskId = taskId;
        task.CreatedByUserId = owner;
        db.CalendarEventMetadata.Add(calendar);
        db.TodoTaskMetadata.Add(task);
    }

    private static GoogleCalendarService createCalendarService(TestDbContextFactory factory, MemoryCache cache, CredentialProbe credential)
    {
        PermissionService permissions = new PermissionService(factory, new FakeDriveService(), cache, new OrganizationPermissionService(factory));
        return new GoogleCalendarService(credential, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), permissions);
    }

    private static GoogleTasksTodoService createTodoService(TestDbContextFactory factory, CredentialProbe credential)
    {
        return new GoogleTasksTodoService(factory, credential, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), new OrganizationPermissionService(factory));
    }

    private static Task executeCalendarAsync(GoogleCalendarService service, string operation)
    {
        UserSession session = OperationPermissionRegressionTests.createSession();
        if (operation == "delete")
        {
            return service.DeleteEventAsync(session, CalendarId.CreateOptional("calendar"), CalendarEventId.CreateOptional("event"), "\"baseline\"", CancellationToken.None);
        }

        CalendarEventFormModel form = new CalendarEventFormModel();
        form.ETag = "\"baseline\"";
        form.CalendarId = "calendar";
        form.Id = "event";
        form.Summary = "본인 일정 수정";
        form.StartLocal = new DateTime(2026, 10, 19, 9, 0, 0);
        form.EndLocal = form.StartLocal.AddHours(1);
        return service.UpdateEventAsync(session, form, CalendarName.CreateOptional("검증"), CancellationToken.None);
    }

    private static Task executeTodoAsync(GoogleTasksTodoService service, string operation)
    {
        UserSession session = OperationPermissionRegressionTests.createSession();
        TodoTaskListId list = TodoTaskListId.CreateOptional("tasks");
        TodoTaskId task = TodoTaskId.CreateOptional("task");
        switch (operation)
        {
            case "delete":
                return service.DeleteTaskAsync(session, list, task, "\"baseline\"", CancellationToken.None);
            case "complete":
                return service.CompleteTaskAsync(session, list, task, CancellationToken.None);
            case "reopen":
                return service.ReopenTaskAsync(session, list, task, CancellationToken.None);
            case "move":
                return service.MoveTaskAsync(session, list, task, TodoTaskId.Empty, CancellationToken.None);
            default:
                TodoTaskFormModel form = new TodoTaskFormModel();
                form.ETag = "\"baseline\"";
                form.TaskListId = "tasks";
                form.TaskId = "task";
                form.Title = "본인 할 일 수정";
                return service.UpdateTaskAsync(session, form, CancellationToken.None);
        }
    }
}
