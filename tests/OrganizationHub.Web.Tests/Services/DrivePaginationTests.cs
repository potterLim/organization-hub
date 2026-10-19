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
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DrivePaginationTests
{
    private sealed class Transport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        public List<string> Pages { get; } = new List<string>();
        public bool ShouldFailSecond
        {
            get; set;
        }
        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken) => Task.FromResult<IConfigurableHttpClientInitializer>(this);
        public void Initialize(ConfigurableHttpClient client)
        {
            client.MessageHandler.InnerHandler = this;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Microsoft.Extensions.Primitives.StringValues token;
            QueryHelpers.ParseQuery(request.RequestUri!.Query).TryGetValue("pageToken", out token);
            string page = token.ToString();
            Pages.Add(page);
            if (page == "second" && ShouldFailSecond)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            }
            string json = page == string.Empty
                ? "{\"files\":[{\"id\":\"recent\",\"name\":\"archived-recent.pdf\",\"mimeType\":\"application/pdf\"}],\"nextPageToken\":\"second\"}"
                : page == "second" ? "{\"files\":[],\"nextPageToken\":\"third\"}" : "{\"files\":[{\"id\":\"old\",\"name\":\"archived.pdf\",\"mimeType\":\"application/pdf\"}]}";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData("list")]
    [InlineData("search")]
    [InlineData("conflict")]
    public async Task FolderOperationsIncludeAllPagesIncludingEmptyIntermediatePageAsync(string operation)
    {
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            Transport transport = new Transport();
            GoogleDriveService service = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            DriveFolderId folder = DriveFolderId.CreateRequired("folder");
            if (operation == "conflict")
            {
                DriveFileNameConflictCheckResult conflict = await service.CheckFileNameConflictAsync(folder, DriveItemName.CreateRequired("archived.pdf"), CancellationToken.None);
                Assert.True(conflict.HasConflict);
                Assert.Equal("old", conflict.GetFirstExistingFileId().GetRequiredValue());
            }
            else
            {
                IReadOnlyList<DriveFileItem> files = operation == "list"
                    ? await service.ListFilesAsync(folder, CancellationToken.None)
                    : await service.SearchFilesAsync(folder, SearchKeyword.CreateOptional("archived"), CancellationToken.None);
                Assert.Equal(new string[] { "recent", "old" }, files.Select(file => file.Id));
            }
            Assert.Equal(new string[] { "", "second", "third" }, transport.Pages);
        }
    }

    [Fact]
    public async Task FailedLaterPageIsNotCachedAsCompleteFolderAsync()
    {
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            Transport transport = new Transport();
            transport.ShouldFailSecond = true;
            GoogleDriveService service = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            DriveFolderId folder = DriveFolderId.CreateRequired("folder");
            await Assert.ThrowsAsync<Google.GoogleApiException>(() => service.ListFilesAsync(folder, CancellationToken.None));
            transport.ShouldFailSecond = false;
            Assert.Equal(2, (await service.ListFilesAsync(folder, CancellationToken.None)).Count);
            Assert.Equal(new string[] { "", "second", "", "second", "third" }, transport.Pages);
        }
    }
}
