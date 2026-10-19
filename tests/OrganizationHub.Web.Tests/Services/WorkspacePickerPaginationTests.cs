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
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class WorkspacePickerPaginationTests
{
    private sealed class Transport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        public List<string> Pages { get; } = new List<string>();
        public bool ShouldFailSecond { get; set; }
        public int Disposals { get; private set; }
        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken) => Task.FromResult<IConfigurableHttpClientInitializer>(this);
        public void Initialize(ConfigurableHttpClient client)
        {
            client.MessageHandler.InnerHandler = this;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            IDictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            string page = query.ContainsKey("pageToken") ? query["pageToken"].ToString() : string.Empty;
            Pages.Add(page);
            if (page == "second" && ShouldFailSecond)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            }
            string key = request.RequestUri.AbsolutePath.Contains("drive") ? "files" : "items";
            string item = page == "second" ? "" : "{\"id\":\"" + (page == "" ? "first" : "last") + "\",\"name\":\"Folder\",\"summary\":\"Calendar\",\"title\":\"Tasks\"}";
            string next = page == "" ? "second" : page == "second" ? "third" : "";
            string token = next != "" && query["fields"].ToString().Contains("nextPageToken") ? ",\"nextPageToken\":\"" + next + "\"" : "";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent("{\"" + key + "\":[" + item + "]" + token + "}", Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }
        }
    }

    [Theory]
    [InlineData("folders")]
    [InlineData("calendars")]
    [InlineData("tasks")]
    public async Task PickersFollowAllPagesAndCacheOnlyCompleteResultsAsync(string kind)
    {
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            Transport transport = new Transport();
            GoogleWorkspaceBrowserService service = new GoogleWorkspaceBrowserService(transport, Options.Create(new GoogleApiOptions()), cache);
            transport.ShouldFailSecond = true;
            await Assert.ThrowsAsync<Google.GoogleApiException>(() => listIdsAsync(service, kind));
            Assert.True(transport.Disposals > 0);
            transport.ShouldFailSecond = false;
            Assert.Equal(new string[] { "first", "last" }, await listIdsAsync(service, kind));
            int requests = transport.Pages.Count;
            Assert.Equal(new string[] { "first", "last" }, await listIdsAsync(service, kind));
            Assert.Equal(requests, transport.Pages.Count);
            Assert.Equal(new string[] { "", "second", "", "second", "third" }, transport.Pages);
            Assert.Equal(2, transport.Disposals);
        }
    }

    private static async Task<IReadOnlyList<string>> listIdsAsync(GoogleWorkspaceBrowserService service, string kind)
    {
        switch (kind)
        {
            case "folders":
                return (await service.ListDriveFoldersAsync(DriveFolderId.Root, CancellationToken.None)).Select(item => item.Id).ToList();
            case "calendars":
                return (await service.ListCalendarsAsync(CancellationToken.None)).Select(item => item.Id).ToList();
            case "tasks":
                return (await service.ListTaskListsAsync(CancellationToken.None)).Select(item => item.Id).ToList();
            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
