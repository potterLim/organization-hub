using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CalendarPaginationTests
{
    private sealed class CalendarTransport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        public List<string> PageTokens { get; } = new List<string>();
        public bool ShouldFailSecondPage
        {
            get; set;
        }
        public CancellationTokenSource? CancellationOrNull
        {
            get; set;
        }

        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
        {
            return Task.FromResult<IConfigurableHttpClientInitializer>(this);
        }

        public void Initialize(ConfigurableHttpClient httpClient)
        {
            httpClient.MessageHandler.InnerHandler = this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotNull(request.RequestUri);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            string pageToken = query.ContainsKey("pageToken") ? query["pageToken"].ToString() : string.Empty;
            PageTokens.Add(pageToken);
            Assert.Equal("true", query["singleEvents"].ToString());
            Assert.Equal("startTime", query["orderBy"].ToString());
            Assert.Contains("2026-10-01", query["timeMin"].ToString());
            Assert.Contains("2026-11-01", query["timeMax"].ToString());
            if (pageToken == "second" && ShouldFailSecondPage)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            }

            string json;
            if (pageToken == string.Empty)
            {
                json = "{\"items\":[{\"id\":\"first\",\"summary\":\"첫 일정\",\"start\":{\"date\":\"2026-10-19\"},\"end\":{\"date\":\"2026-10-20\"}}],\"nextPageToken\":\"second\"}";
                CancellationOrNull?.Cancel();
            }
            else if (pageToken == "second")
            {
                json = "{\"items\":[],\"nextPageToken\":\"third\"}";
            }
            else
            {
                Assert.Equal("third", pageToken);
                json = "{\"items\":[{\"id\":\"last\",\"summary\":\"마지막 일정\",\"start\":{\"date\":\"2026-10-20\"},\"end\":{\"date\":\"2026-10-21\"}}]}";
            }

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadsEveryPageIncludingEmptyIntermediatePageAndCachesCompleteResultAsync(bool isAggregate)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (CalendarTransport transport = new CalendarTransport())
        {
            GoogleCalendarService service = createService(factory, cache, transport);
            IReadOnlyList<CalendarEventItem> events = await readAsync(service, isAggregate, CancellationToken.None);
            Assert.Equal(new string[] { "first", "last" }, events.Select(item => item.Id));
            Assert.Equal(new string[] { "", "second", "third" }, transport.PageTokens);
            events[0].Summary = "클라이언트 변경";
            IReadOnlyList<CalendarEventItem> cached = await readAsync(service, isAggregate, CancellationToken.None);
            Assert.Equal("첫 일정", cached[0].Summary);
            Assert.Equal(3, transport.PageTokens.Count);
        }
    }

    [Fact]
    public async Task FailedLaterPageDoesNotCachePartialResultsAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (CalendarTransport transport = new CalendarTransport())
        {
            transport.ShouldFailSecondPage = true;
            GoogleCalendarService service = createService(factory, cache, transport);
            await Assert.ThrowsAsync<Google.GoogleApiException>(() => readAsync(service, false, CancellationToken.None));
            transport.ShouldFailSecondPage = false;
            IReadOnlyList<CalendarEventItem> events = await readAsync(service, false, CancellationToken.None);
            Assert.Equal(2, events.Count);
            Assert.Equal(new string[] { "", "second", "", "second", "third" }, transport.PageTokens);
        }
    }

    [Fact]
    public async Task CancellationBetweenPagesDoesNotReturnPartialResultsAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (CalendarTransport transport = new CalendarTransport())
        using (CancellationTokenSource cancellation = new CancellationTokenSource())
        {
            transport.CancellationOrNull = cancellation;
            GoogleCalendarService service = createService(factory, cache, transport);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readAsync(service, false, cancellation.Token));
            transport.CancellationOrNull = null;
            IReadOnlyList<CalendarEventItem> events = await readAsync(service, false, CancellationToken.None);
            Assert.Equal(2, events.Count);
        }
    }

    private static GoogleCalendarService createService(TestDbContextFactory factory, MemoryCache cache, CalendarTransport transport)
    {
        FakeDriveService drive = new FakeDriveService();
        OrganizationPermissionService organizationPermissions = new OrganizationPermissionService(factory);
        PermissionService permissions = new PermissionService(factory, drive, cache, organizationPermissions);
        return new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), permissions);
    }

    private static Task<IReadOnlyList<CalendarEventItem>> readAsync(GoogleCalendarService service, bool isAggregate, CancellationToken cancellationToken)
    {
        CalendarEventDateRange range = new CalendarEventDateRange(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));
        UserSession session = OperationPermissionRegressionTests.createSession();
        if (isAggregate)
        {
            return service.GetAggregateEventsAsync(session, range, cancellationToken);
        }

        return service.GetEventsAsync(session, CalendarId.CreateOptional("calendar"), CalendarName.CreateOptional("검증"), range, cancellationToken);
    }
}
