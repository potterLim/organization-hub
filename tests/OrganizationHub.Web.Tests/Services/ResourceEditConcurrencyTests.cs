using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public sealed class ResourceEditConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GoogleSdkHiddenContainerResponseDoesNotEraseOwnershipAsync(bool isCalendar)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(isCalendar ? PermissionKeys.CALENDAR_DELETE : PermissionKeys.TODOS_DELETE))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                db.CalendarEventMetadata.Add(new CalendarEventMetadata { CalendarId = "calendar", EventId = "item", CreatedByUserId = 1 });
                db.TodoTaskMetadata.Add(new TodoTaskMetadata { TaskListId = "tasks", TaskId = "item", CreatedByUserId = 1 });
                await db.SaveChangesAsync();
            }
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            transport.ShouldHideContainer = true;
            OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
            UserSession session = OperationPermissionRegressionTests.createSession();
            if (isCalendar)
            {
                GoogleCalendarService calendar = new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), new PermissionService(factory, new FakeDriveService(), cache, permissions));
                await Assert.ThrowsAsync<ResourceDeletionUncertainException>(() => calendar.DeleteEventAsync(session, CalendarId.CreateRequired("calendar"), CalendarEventId.CreateRequired("item"), transport.ETag, CancellationToken.None));
            }
            else
            {
                GoogleTasksTodoService todos = new GoogleTasksTodoService(factory, transport, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), permissions);
                await Assert.ThrowsAsync<ResourceDeletionUncertainException>(() => todos.DeleteTaskAsync(session, TodoTaskListId.CreateRequired("tasks"), TodoTaskId.CreateRequired("item"), transport.ETag, CancellationToken.None));
            }
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.True(db.CalendarEventMetadata.Any(item => item.EventId == "item"));
                Assert.True(db.TodoTaskMetadata.Any(item => item.TaskId == "item"));
            }
            Assert.Equal(0, transport.Deletes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostDeleteResponseIsConfirmedWithoutDeletingAgainAsync(bool isCalendar)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(isCalendar ? PermissionKeys.CALENDAR_DELETE : PermissionKeys.TODOS_DELETE))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            transport.ShouldLoseDeleteResponse = true;
            OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
            UserSession session = OperationPermissionRegressionTests.createSession();
            if (isCalendar)
            {
                GoogleCalendarService calendar = new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), new PermissionService(factory, new FakeDriveService(), cache, permissions));
                await calendar.DeleteEventAsync(session, CalendarId.CreateRequired("calendar"), CalendarEventId.CreateRequired("item"), transport.ETag, CancellationToken.None);
            }
            else
            {
                GoogleTasksTodoService todos = new GoogleTasksTodoService(factory, transport, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), permissions);
                await todos.DeleteTaskAsync(session, TodoTaskListId.CreateRequired("tasks"), TodoTaskId.CreateRequired("item"), transport.ETag, CancellationToken.None);
            }
            Assert.Equal(1, transport.Deletes);
        }
    }

    [Fact]
    public async Task ExternalCalendarConflictReloadsTheCurrentVersionInsteadOfCachedContentAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                PositionPermissionGrant grant = new PositionPermissionGrant();
                grant.PositionId = 1;
                grant.PermissionKey = PermissionKeys.CALENDAR_DELETE;
                grant.ScopeType = EPermissionScopeType.Global;
                grant.IsAllowed = true;
                db.PositionPermissionGrants.Add(grant);
                await db.SaveChangesAsync();
            }
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
            GoogleCalendarService calendar = new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), new PermissionService(factory, new FakeDriveService(), cache, permissions));
            UserSession session = OperationPermissionRegressionTests.createSession();
            CalendarId id = CalendarId.CreateRequired("calendar");
            CalendarName name = CalendarName.CreateOptional("Calendar");
            CalendarEventDateRange range = new CalendarEventDateRange(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));
            CalendarEventItem original = Assert.Single(await calendar.GetEventsAsync(session, id, name, range, CancellationToken.None));
            using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient(transport, false))
            {
                await client.PatchAsync("https://example.test/item", new System.Net.Http.StringContent("{\"summary\":\"External change\"}"));
            }
            await Assert.ThrowsAsync<ResourceEditConflictException>(() => calendar.DeleteEventAsync(session, id, CalendarEventId.CreateRequired("item"), original.ETag, CancellationToken.None));
            Assert.Equal("External change", Assert.Single(await calendar.GetEventsAsync(session, id, name, range, CancellationToken.None)).Summary);
            Assert.Equal(0, transport.Deletes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleDeleteCannotRemoveAnotherWritersSavedChangeAsync(bool isCalendar)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(isCalendar ? PermissionKeys.CALENDAR_DELETE : PermissionKeys.TODOS_DELETE))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            string originalETag = transport.ETag;
            using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient(transport, false))
            {
                await client.PatchAsync("https://example.test/item", new System.Net.Http.StringContent("{\"title\":\"Another writer\"}"));
            }
            UserSession session = OperationPermissionRegressionTests.createSession();
            OrganizationPermissionService organization = new OrganizationPermissionService(factory);
            if (isCalendar)
            {
                GoogleCalendarService calendar = new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), new PermissionService(factory, new FakeDriveService(), cache, organization));
                await Assert.ThrowsAsync<ResourceEditConflictException>(() => calendar.DeleteEventAsync(session, CalendarId.CreateRequired("calendar"), CalendarEventId.CreateRequired("item"), originalETag, CancellationToken.None));
                Assert.Equal(0, transport.Deletes);
                await calendar.DeleteEventAsync(session, CalendarId.CreateRequired("calendar"), CalendarEventId.CreateRequired("item"), transport.ETag, CancellationToken.None);
            }
            else
            {
                GoogleTasksTodoService todos = new GoogleTasksTodoService(factory, transport, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), organization);
                await Assert.ThrowsAsync<ResourceEditConflictException>(() => todos.DeleteTaskAsync(session, TodoTaskListId.CreateRequired("tasks"), TodoTaskId.CreateRequired("item"), originalETag, CancellationToken.None));
                Assert.Equal(0, transport.Deletes);
                await todos.DeleteTaskAsync(session, TodoTaskListId.CreateRequired("tasks"), TodoTaskId.CreateRequired("item"), transport.ETag, CancellationToken.None);
            }
            Assert.Equal(1, transport.Deletes);
            Assert.Equal("Another writer", transport.Title);
            Assert.NotEqual(originalETag, transport.ETag);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleResourceEditsCannotOverwriteTheFirstWritersTitleAsync(bool isCalendar)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(isCalendar ? PermissionKeys.CALENDAR_UPDATE : PermissionKeys.TODOS_UPDATE))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            UserSession session = OperationPermissionRegressionTests.createSession();
            OrganizationPermissionService organization = new OrganizationPermissionService(factory);
            GoogleTasksTodoService todos = new GoogleTasksTodoService(factory, transport, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), organization);
            GoogleCalendarService calendar = new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), new PermissionService(factory, new FakeDriveService(), cache, organization));
            if (isCalendar)
            {
                using (OrganizationHubDbContext db = factory.CreateDbContext())
                {
                    CalendarEventMetadata owner = new CalendarEventMetadata();
                    owner.CalendarId = "calendar";
                    owner.EventId = "item";
                    owner.CreatedByUserId = 1;
                    owner.CreatedByDisplayName = "Original owner";
                    db.CalendarEventMetadata.Add(owner);
                    await db.SaveChangesAsync();
                }
            }
            string originalETag = transport.ETag;
            async Task updateAsync(string title, string etag)
            {
                if (isCalendar)
                {
                    CalendarEventFormModel form = new CalendarEventFormModel();
                    form.Id = "item";
                    form.CalendarId = "calendar";
                    form.Summary = title;
                    form.ETag = etag;
                    form.StartLocal = new DateTime(2026, 10, 19, 9, 0, 0);
                    form.EndLocal = form.StartLocal.AddHours(1);
                    CalendarEventItem saved = await calendar.UpdateEventAsync(session, form, CalendarName.CreateOptional("Calendar"), CancellationToken.None);
                    Assert.True(saved.CanEdit);
                    Assert.Equal(1, saved.CreatedByUserIdOrNull);
                    Assert.Equal(transport.ETag, saved.ETag);
                }
                else
                {
                    TodoTaskFormModel form = new TodoTaskFormModel();
                    form.TaskId = "item";
                    form.TaskListId = "tasks";
                    form.Title = title;
                    form.ETag = etag;
                    await todos.UpdateTaskAsync(session, form, CancellationToken.None);
                }
            }
            await updateAsync("First writer", originalETag);
            await Assert.ThrowsAsync<ResourceEditConflictException>(() => updateAsync("Original", originalETag));
            Assert.Equal("First writer", transport.Title);
            Assert.Equal(1, transport.Writes);
            if (isCalendar == false)
            {
                await todos.RecoverDeadlineChangesAsync(CancellationToken.None);
                using (OrganizationHubDbContext db = factory.CreateDbContext())
                {
                    Assert.Empty(db.TodoDeadlineChanges);
                }
                Assert.Equal(1, transport.Writes);
            }
            await updateAsync("Reloaded edit", transport.ETag);
            Assert.Equal("Reloaded edit", transport.Title);
        }
    }
}
