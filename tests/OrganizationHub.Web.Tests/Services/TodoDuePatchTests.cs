using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TodoDuePatchTests
{
    private sealed class Transport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        public string Body { get; private set; } = string.Empty;
        public string? StoredDueOrNull { get; private set; } = "2026-10-20T00:00:00Z";
        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken) => Task.FromResult<IConfigurableHttpClientInitializer>(this);
        public void Initialize(ConfigurableHttpClient client)
        {
            client.MessageHandler.InnerHandler = this;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > 1 && bytes[0] == 31 && bytes[1] == 139)
            {
                using (GZipStream gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
                using (StreamReader reader = new StreamReader(gzip))
                {
                    Body = await reader.ReadToEndAsync(cancellationToken);
                }
            }
            else
            {
                Body = Encoding.UTF8.GetString(bytes);
            }
            using (JsonDocument json = JsonDocument.Parse(Body))
            {
                JsonElement due;
                if (json.RootElement.TryGetProperty("due", out due))
                {
                    StoredDueOrNull = due.ValueKind == JsonValueKind.Null ? null : due.GetString();
                }
            }
            string responseJson = "{\"id\":\"task\",\"title\":\"Updated\",\"status\":\"needsAction\",\"due\":" + JsonSerializer.Serialize(StoredDueOrNull) + "}";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(responseJson, Encoding.UTF8, "application/json");
            return response;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DueDatePatchExplicitlyClearsOrReplacesGoogleValueAsync(bool shouldClear)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.TODOS_UPDATE))
        {
            Transport transport = new Transport();
            GoogleTasksTodoService service = new GoogleTasksTodoService(factory, transport, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), new OrganizationPermissionService(factory));
            TodoTaskFormModel form = new TodoTaskFormModel(); form.ETag = "\"baseline\"";
            form.TaskListId = "tasks";
            form.TaskId = "task";
            form.Title = "Updated";
            form.DueAtUtc = shouldClear ? null : new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
            TodoTaskItem item = await service.UpdateTaskAsync(OperationPermissionRegressionTests.createSession(), form, CancellationToken.None);
            using (JsonDocument body = JsonDocument.Parse(transport.Body))
            {
                Assert.True(body.RootElement.TryGetProperty("due", out JsonElement due));
                Assert.Equal(shouldClear ? JsonValueKind.Null : JsonValueKind.String, due.ValueKind);
            }
            Assert.Equal(shouldClear, transport.StoredDueOrNull == null);
            Assert.Equal(form.DueAtUtc, item.DueAt);
        }
    }
}
