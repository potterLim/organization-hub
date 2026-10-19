using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CreatorMetadataPersistenceTests
{
    private sealed class MetadataFailure : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (data.Context!.ChangeTracker.Entries<CalendarEventMetadata>().Any(entry => entry.State == EntityState.Added) || data.Context.ChangeTracker.Entries<TodoTaskMetadata>().Any(entry => entry.State == EntityState.Added))
            {
                throw new IOException("Creator metadata storage failed.");
            }
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }

    private sealed class Transport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        public int Creates { get; private set; }
        public int Deletes { get; private set; }
        public bool ShouldFailRollback { get; set; }
        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken) => Task.FromResult<IConfigurableHttpClientInitializer>(this);
        public void Initialize(ConfigurableHttpClient client) { client.MessageHandler.InnerHandler = this; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post) { Creates++; }
            if (request.Method == HttpMethod.Delete)
            {
                Deletes++;
                return Task.FromResult(new HttpResponseMessage(ShouldFailRollback ? HttpStatusCode.Forbidden : HttpStatusCode.NoContent));
            }
            string json = "{\"id\":\"created\",\"title\":\"Created\",\"summary\":\"Created\",\"status\":\"needsAction\",\"start\":{\"dateTime\":\"2026-10-19T09:00:00Z\"},\"end\":{\"dateTime\":\"2026-10-19T10:00:00Z\"}}";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MetadataFailureDoesNotReportSuccessAndCompensatesRemoteCreationAsync(bool isCalendar, bool shouldFailRollback)
    {
        using (TestDbContextFactory raw = new TestDbContextFactory(new MetadataFailure()))
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(isCalendar ? PermissionKeys.CALENDAR_CREATE : PermissionKeys.TODOS_CREATE, raw))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            Transport transport = new Transport();
            transport.ShouldFailRollback = shouldFailRollback;
            UserSession session = OperationPermissionRegressionTests.createSession();
            Exception error = await Assert.ThrowsAnyAsync<Exception>(createAsync);
            Assert.Contains(shouldFailRollback ? "관리자" : "취소", error.Message);
            Assert.Equal(1, transport.Creates);
            Assert.Equal(1, transport.Deletes);
            using (OrganizationHub.Web.Data.OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.CalendarEventMetadata);
                Assert.Empty(db.TodoTaskMetadata);
            }

            async Task createAsync()
            {
                if (isCalendar)
                {
                    PermissionService permissions = new PermissionService(factory, new FakeDriveService(), cache, new OrganizationPermissionService(factory));
                    GoogleCalendarService calendar = new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), permissions);
                    CalendarEventFormModel form = new CalendarEventFormModel();
                    form.CalendarId = "calendar";
                    form.Summary = "Created";
                    form.StartLocal = new DateTime(2026, 10, 19, 9, 0, 0);
                    form.EndLocal = form.StartLocal.AddHours(1);
                    await calendar.CreateEventAsync(session, form, CalendarName.CreateOptional("Calendar"), CancellationToken.None);
                }
                else
                {
                    GoogleTasksTodoService todos = new GoogleTasksTodoService(factory, transport, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), new OrganizationPermissionService(factory));
                    TodoTaskFormModel form = new TodoTaskFormModel();
                    form.TaskListId = "tasks";
                    form.Title = "Created";
                    await todos.CreateTaskAsync(session, form, CancellationToken.None);
                }
            }
        }
    }
}
