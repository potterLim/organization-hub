using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Tests.Fakes;

public sealed class CalendarContractTransport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
{
    private JsonObject mEvent = JsonNode.Parse("{\"id\":\"period\",\"etag\":\"\\\"v1\\\"\",\"summary\":\"종일 행사\",\"start\":{\"date\":\"2026-10-19\"},\"end\":{\"date\":\"2026-10-20\"}}")!.AsObject();

    public string ExclusiveEndDate
    {
        get
        {
            return mEvent["end"]!["date"]!.GetValue<string>();
        }
    }

    public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
    {
        return Task.FromResult<IConfigurableHttpClientInitializer>(this);
    }

    public void Initialize(ConfigurableHttpClient client)
    {
        client.MessageHandler.InnerHandler = this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Patch)
        {
            byte[] bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            using (MemoryStream content = new MemoryStream(bytes))
            using (Stream decoded = bytes.Length > 1 && bytes[0] == 31 && bytes[1] == 139 ? new GZipStream(content, CompressionMode.Decompress) : content)
            using (StreamReader reader = new StreamReader(decoded))
            {
                JsonObject patch = JsonNode.Parse(await reader.ReadToEndAsync(cancellationToken))!.AsObject();
                mEvent["start"] = patch["start"]!.DeepClone();
                mEvent["end"] = patch["end"]!.DeepClone();
            }
        }

        string json = request.RequestUri!.AbsolutePath.EndsWith("/events", System.StringComparison.Ordinal)
            ? "{\"items\":[" + mEvent.ToJsonString() + "]}" : mEvent.ToJsonString();
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return response;
    }
}
