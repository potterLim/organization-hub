using System;
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
