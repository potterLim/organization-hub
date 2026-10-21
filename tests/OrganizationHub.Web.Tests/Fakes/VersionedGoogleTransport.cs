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

public sealed class VersionedGoogleTransport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
{
    private int mVersion = 1;
    private string mStatus = "needsAction";
    public HttpStatusCode? RejectedPatchStatusOrNull
    {
        get; set;
    }
    public int Deletes
    {
        get; private set;
    }
    public int Writes
    {
        get; private set;
    }
    public string Title { get; private set; } = "Original";
    public string ETag
    {
        get
        {
            return "\"v" + mVersion + "\"";
        }
    }
    public string? DueOrNull { get; private set; } = "2026-10-20T00:00:00Z";
    public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken) => Task.FromResult<IConfigurableHttpClientInitializer>(this);
    public void Initialize(ConfigurableHttpClient client)
    {
        client.MessageHandler.InnerHandler = this;
        client.MessageHandler.NumTries = 1;
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if ((request.Method == HttpMethod.Patch || request.Method == HttpMethod.Delete) && request.Headers.IfMatch.Count > 0 && request.Headers.IfMatch.ToString() != ETag)
        {
            return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
        }
        if (request.Method == HttpMethod.Delete)
        {
            Deletes++;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
        if (request.Method == HttpMethod.Patch)
        {
            if (RejectedPatchStatusOrNull.HasValue)
            {
                return new HttpResponseMessage(RejectedPatchStatusOrNull.Value);
            }
            if (request.Headers.IfMatch.Count > 0 && request.Headers.IfMatch.ToString() != ETag)
            {
                return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
            }
            byte[] bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            string body;
            if (bytes.Length > 1 && bytes[0] == 31 && bytes[1] == 139)
            {
                using (GZipStream gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
                using (StreamReader reader = new StreamReader(gzip))
                {
                    body = await reader.ReadToEndAsync(cancellationToken);
                }
            }
            else
            {
                body = Encoding.UTF8.GetString(bytes);
            }
            JsonObject json = JsonNode.Parse(body)!.AsObject();
            if (json.ContainsKey("title"))
            {
                Title = json["title"]!.GetValue<string>();
            }
            if (json.ContainsKey("summary"))
            {
                Title = json["summary"]!.GetValue<string>();
            }
            if (json.ContainsKey("due"))
            {
                DueOrNull = json["due"]?.GetValue<string>();
                if (DueOrNull != null)
                {
                    DueOrNull = DueOrNull.Substring(0, 10) + "T00:00:00Z";
                }
            }
            if (json.ContainsKey("status"))
            {
                mStatus = json["status"]!.GetValue<string>();
            }
            Writes++;
            mVersion++;
        }
        JsonObject result = new JsonObject();
        result["id"] = "item";
        result["etag"] = ETag;
        result["title"] = Title;
        result["summary"] = Title;
        result["status"] = mStatus;
        result["due"] = DueOrNull;
        result["start"] = JsonNode.Parse("{\"dateTime\":\"2026-10-19T09:00:00Z\"}");
        result["end"] = JsonNode.Parse("{\"dateTime\":\"2026-10-19T10:00:00Z\"}");
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        string jsonResult = result.ToJsonString();
        if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/tasks", System.StringComparison.Ordinal))
        {
            jsonResult = "{\"items\":[" + jsonResult + "]}";
        }
        response.Content = new StringContent(jsonResult, Encoding.UTF8, "application/json");
        return response;
    }
}
