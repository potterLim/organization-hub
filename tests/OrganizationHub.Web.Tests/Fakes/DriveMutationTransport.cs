using System;
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

public sealed class DriveMutationTransport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
{
    private JsonObject mFile = new JsonObject
    {
        ["id"] = "file",
        ["name"] = "report.pdf",
        ["mimeType"] = "application/pdf",
        ["parents"] = new JsonArray("folder"),
        ["trashed"] = false
    };
    private JsonObject mPendingMetadata = new JsonObject();
    public bool HasFile { get; set; } = true;
    public bool ShouldCommit { get; set; } = true;
    public bool ShouldLoseResponse { get; set; } = true;
    public bool ShouldFailRead
    {
        get; set;
    }
    public bool ShouldHideParent
    {
        get; set;
    }
    public bool ShouldReturnMissingAfterWrite
    {
        get; set;
    }
    public HttpStatusCode? RejectedStatusOrNull
    {
        get; set;
    }
    public int Writes
    {
        get; private set;
    }
    public bool IsTrashed
    {
        get; private set;
    }
    public string OperationKey { get; private set; } = string.Empty;
    public string UploadedFileId { get; private set; } = string.Empty;

    public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken) => Task.FromResult<IConfigurableHttpClientInitializer>(this);

    public void Initialize(ConfigurableHttpClient client)
    {
        client.MessageHandler.InnerHandler = this;
        client.MessageHandler.NumTries = 1;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string absolutePath = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get)
        {
            if (absolutePath.EndsWith("/generateIds", StringComparison.Ordinal))
            {
                return jsonResponse("{\"ids\":[\"reserved-file\"]}");
            }
            if (absolutePath.EndsWith("/folder", StringComparison.Ordinal))
            {
                return ShouldHideParent ? new HttpResponseMessage(HttpStatusCode.NotFound) : jsonResponse("{\"id\":\"folder\"}");
            }
            if (ShouldFailRead && Writes > 0)
            {
                throw new HttpRequestException("Read unavailable after sending the mutation.");
            }
            if (absolutePath.EndsWith("/files", StringComparison.Ordinal))
            {
                return jsonResponse("{\"files\":" + (HasFile && IsTrashed == false ? "[" + mFile.ToJsonString() + "]" : "[]") + "}");
            }
            if (HasFile == false || (ShouldReturnMissingAfterWrite && Writes > 0))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            return jsonResponse(mFile.ToJsonString());
        }
        if (request.Method == HttpMethod.Put)
        {
            if (request.Content?.Headers.ContentRange?.HasRange == false)
            {
                return jsonResponse(mFile.ToJsonString());
            }
            Writes++;
            if (ShouldCommit)
            {
                mFile = (JsonObject)mPendingMetadata.DeepClone();
                mFile["mimeType"] = "application/pdf";
                mFile["trashed"] = false;
                if (mFile["id"] == null)
                {
                    mFile["id"] = "file";
                }
                UploadedFileId = mFile["id"]!.GetValue<string>();
                OperationKey = mFile["appProperties"]!["organizationHubUpload"]!.GetValue<string>();
                HasFile = true;
            }
            if (ShouldLoseResponse)
            {
                throw new HttpRequestException("Upload completed but its response was lost.");
            }
            return jsonResponse(mFile.ToJsonString());
        }
        JsonObject body = (JsonObject)JsonNode.Parse(await readContentAsync(request.Content!, cancellationToken))!;
        if (body["trashed"] != null)
        {
            Writes++;
            if (RejectedStatusOrNull.HasValue)
            {
                return new HttpResponseMessage(RejectedStatusOrNull.Value);
            }
            if (ShouldCommit)
            {
                IsTrashed = true;
                mFile["trashed"] = true;
            }
            if (ShouldLoseResponse)
            {
                throw new HttpRequestException("Trash update completed but its response was lost.");
            }
            return jsonResponse(mFile.ToJsonString());
        }
        mPendingMetadata = body;
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Location = new Uri("https://upload.example.test/session");
        return response;
    }

    private static HttpResponseMessage jsonResponse(string json)
    {
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return response;
    }

    private static async Task<string> readContentAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentEncoding.Contains("gzip"))
        {
            using (Stream stream = await content.ReadAsStreamAsync(cancellationToken))
            using (GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(gzip))
            {
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
        return await content.ReadAsStringAsync(cancellationToken);
    }
}
