using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class GoogleApprovalPublicationTests
{
    private sealed class DriveTransport : IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        private sealed class Handler : HttpMessageHandler
        {
            private readonly DriveTransport mTransport;

            public Handler(DriveTransport transport)
            {
                mTransport = transport;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return mTransport.handleAsync(request, cancellationToken);
            }
        }

        private string mOperationKey = string.Empty;
        private string mPendingOperationKey = string.Empty;
        private string mFileName = "document.pdf";
        private long mVersion = 1;

        public byte[] Content { get; set; } = Encoding.UTF8.GetBytes("original document");
        public long? ReportedSizeOrNull
        {
            get; set;
        }
        public int DownloadCount
        {
            get; private set;
        }
        public bool HasFile
        {
            get; set;
        }
        public bool ShouldLoseUploadResponse
        {
            get; set;
        }
        public bool ShouldChangeDuringDownload
        {
            get; set;
        }
        public int UploadCount
        {
            get; private set;
        }

        public bool ShouldCommitBeforeCreateConflict
        {
            get; set;
        }
        public string MimeType { get; set; } = "application/pdf";

        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
        {
            return Task.FromResult<IConfigurableHttpClientInitializer>(this);
        }

        public void Initialize(ConfigurableHttpClient httpClient)
        {
            httpClient.MessageHandler.InnerHandler = new Handler(this);
        }

        private async Task<HttpResponseMessage> handleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.NotNull(request.RequestUri);
            if (request.RequestUri.AbsolutePath.EndsWith("/generateIds", StringComparison.Ordinal))
            {
                return jsonResponse(new
                {
                    ids = new string[] { "reserved-file" }
                });
            }

            if (request.Method == HttpMethod.Get && request.RequestUri.Query.Contains("alt=media", StringComparison.Ordinal))
            {
                DownloadCount++;
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(Content);
                if (ShouldChangeDuringDownload)
                {
                    Content = Encoding.UTF8.GetBytes("changed during download");
                    mVersion++;
                }

                return response;
            }

            if (request.Method == HttpMethod.Get)
            {
                return HasFile ? metadataResponse() : new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Patch)
            {
                Assert.NotNull(request.Content);
                using (JsonDocument metadata = JsonDocument.Parse(await readRequestContentAsync(request.Content, cancellationToken)))
                {
                    mPendingOperationKey = metadata.RootElement.GetProperty("appProperties").GetProperty("organizationHubApproval").GetString()!;
                    mFileName = metadata.RootElement.GetProperty("name").GetString()!;
                    if (request.Method == HttpMethod.Post)
                    {
                        Assert.Equal("reserved-file", metadata.RootElement.GetProperty("id").GetString());
                    }
                }

                if (ShouldCommitBeforeCreateConflict)
                {
                    ShouldCommitBeforeCreateConflict = false;
                    mOperationKey = mPendingOperationKey;
                    HasFile = true;
                    UploadCount++;
                    HttpResponseMessage conflict = jsonResponse(new
                    {
                        error = new
                        {
                            code = 409,
                            message = "File already exists"
                        }
                    });
                    conflict.StatusCode = HttpStatusCode.Conflict;
                    return conflict;
                }

                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Location = new Uri("https://upload.example.test/session");
                return response;
            }

            if (request.Method == HttpMethod.Put)
            {
                Assert.NotNull(request.Content);
                if (request.Content.Headers.ContentRange != null && request.Content.Headers.ContentRange.HasRange == false)
                {
                    return metadataResponse();
                }

                Content = await readRequestContentAsync(request.Content, cancellationToken);
                mOperationKey = mPendingOperationKey;
                HasFile = true;
                UploadCount++;
                mVersion++;
                if (ShouldLoseUploadResponse)
                {
                    ShouldLoseUploadResponse = false;
                    throw new IOException("Injected response loss after the remote commit.");
                }

                return metadataResponse();
            }

            throw new InvalidOperationException("Unexpected Drive HTTP request: " + request.Method);
        }

        private HttpResponseMessage metadataResponse()
        {
            return jsonResponse(new
            {
                id = "reserved-file",
                name = mFileName,
                mimeType = MimeType,
                size = ReportedSizeOrNull,
                parents = new string[] { "approved-folder" },
                version = mVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                md5Checksum = MimeType.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal) ? string.Empty : Convert.ToHexString(MD5.HashData(Content)).ToLowerInvariant(),
                appProperties = new Dictionary<string, string> { { "organizationHubApproval", mOperationKey } },
                trashed = false
            });
        }

        private static HttpResponseMessage jsonResponse(object body)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return response;
        }

        private static async Task<byte[]> readRequestContentAsync(HttpContent content, CancellationToken cancellationToken)
        {
            byte[] bytes = await content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Headers.ContentEncoding.Contains("gzip") == false)
            {
                return bytes;
            }

            using (MemoryStream compressed = new MemoryStream(bytes))
            using (GZipStream decompressor = new GZipStream(compressed, CompressionMode.Decompress))
            using (MemoryStream decoded = new MemoryStream())
            {
                await decompressor.CopyToAsync(decoded, cancellationToken);
                return decoded.ToArray();
            }
        }
    }

    [Theory]
    [InlineData(EDriveFileNameConflictResolution.AddNewFile)]
    [InlineData(EDriveFileNameConflictResolution.ReplaceExistingFile)]
    public async Task GoogleUploadReceiptRecoversResponseLossWithoutSecondWriteAsync(EDriveFileNameConflictResolution conflictResolution)
    {
        DriveTransport transport = new DriveTransport();
        transport.HasFile = conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile;
        transport.ShouldLoseUploadResponse = true;
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            DriveItemId fileId = await drive.GenerateFileIdAsync(CancellationToken.None);
            byte[] content = Encoding.UTF8.GetBytes("frozen document");
            ApprovalFilePublishRequest publication = new ApprovalFilePublishRequest
            {
                TargetFileId = fileId,
                TargetFolderId = DriveFolderId.CreateRequired("approved-folder"),
                FileName = DriveItemName.CreateRequired("document.pdf"),
                ContentType = FileContentType.CreateOptional("application/pdf"),
                Content = content,
                OperationKey = "operation-one",
                IsReplacement = conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile
            };

            Exception? firstErrorOrNull = await Record.ExceptionAsync(() => drive.PublishApprovalFileAsync(publication, CancellationToken.None));
            if (firstErrorOrNull != null)
            {
                Assert.IsType<IOException>(firstErrorOrNull);
            }

            Assert.Equal(1, transport.UploadCount);
            Assert.Equal(content, transport.Content);
            DriveFileItem recoveredFile = await drive.PublishApprovalFileAsync(publication, CancellationToken.None);

            Assert.Equal(fileId.GetRequiredValue(), recoveredFile.Id);
            Assert.Equal(content, transport.Content);
            Assert.Equal(1, transport.UploadCount);
            transport.Content = Encoding.UTF8.GetBytes("external modification");
            await Assert.ThrowsAsync<IOException>(() => drive.PublishApprovalFileAsync(publication, CancellationToken.None));
            Assert.Equal(1, transport.UploadCount);
        }
    }

    [Fact]
    public async Task PreallocatedIdConflictReconcilesMatchingFileAsync()
    {
        DriveTransport transport = new DriveTransport();
        transport.ShouldCommitBeforeCreateConflict = true;
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            ApprovalFilePublishRequest publication = new ApprovalFilePublishRequest
            {
                TargetFileId = DriveItemId.CreateRequired("reserved-file"),
                TargetFolderId = DriveFolderId.CreateRequired("approved-folder"),
                FileName = DriveItemName.CreateRequired("document.pdf"),
                ContentType = FileContentType.CreateOptional("application/pdf"),
                Content = transport.Content,
                OperationKey = "operation-one",
                IsReplacement = false
            };

            DriveFileItem file = await drive.PublishApprovalFileAsync(publication, CancellationToken.None);
            Assert.Equal("reserved-file", file.Id);
            Assert.Equal(1, transport.UploadCount);
        }
    }

    [Fact]
    public async Task GoogleDocumentSnapshotIsExportedAsPdfAsync()
    {
        DriveTransport transport = new DriveTransport();
        transport.HasFile = true;
        transport.MimeType = "application/vnd.google-apps.document";
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            FileDownloadResult snapshot = await drive.DownloadFileSnapshotAsync(DriveItemId.CreateRequired("reserved-file"), CancellationToken.None);
            using (Stream content = snapshot.ContentStream)
            {
                Assert.Equal("application/pdf", snapshot.ContentType);
                Assert.EndsWith(".pdf", snapshot.FileName);
                Assert.Equal(transport.Content.Length, content.Length);
            }
        }
    }

    [Fact]
    public async Task SnapshotDownloadBypassesCachedContentAndRejectsMidDownloadChangesAsync()
    {
        DriveTransport transport = new DriveTransport();
        transport.HasFile = true;
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            DriveItemId fileId = DriveItemId.CreateRequired("reserved-file");
            FileDownloadResult cached = await drive.DownloadFileAsync(fileId, CancellationToken.None);
            cached.ContentStream.Dispose();
            transport.Content = Encoding.UTF8.GetBytes("latest submitted document");
            FileDownloadResult fresh = await drive.DownloadFileSnapshotAsync(fileId, CancellationToken.None);
            using (Stream content = fresh.ContentStream)
            using (StreamReader reader = new StreamReader(content))
            {
                Assert.Equal("latest submitted document", await reader.ReadToEndAsync());
            }

            transport.ShouldChangeDuringDownload = true;
            await Assert.ThrowsAsync<IOException>(() => drive.DownloadFileSnapshotAsync(fileId, CancellationToken.None));
        }
    }
}
