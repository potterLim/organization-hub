using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
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

public sealed class DriveDownloadSpoolingTests
{
    private sealed class Transport : HttpMessageHandler, IGoogleApiCredentialFactory, IConfigurableHttpClientInitializer
    {
        public Task<IConfigurableHttpClientInitializer> CreateHttpClientInitializerAsync(string[] scopes, CancellationToken cancellationToken)
        {
            return Task.FromResult<IConfigurableHttpClientInitializer>(this);
        }

        public void Initialize(ConfigurableHttpClient client)
        {
            client.MessageHandler.InnerHandler = this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            if (request.RequestUri!.Query.Contains("alt=media"))
            {
                byte[] bytes = new byte[6 * 1024 * 1024];
                bytes[bytes.Length - 1] = 42;
                response.Content = new ByteArrayContent(bytes);
            }
            else
            {
                response.Content = new StringContent("{\"id\":\"file\",\"name\":\"large.pdf\",\"mimeType\":\"application/pdf\"}", Encoding.UTF8, "application/json");
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task LargeDownloadUsesSeekableTemporaryFileAndDeletesItOnDisposeAsync()
    {
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            GoogleDriveService service = new GoogleDriveService(new Transport(), Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            FileDownloadResult result = await service.DownloadFileAsync(DriveItemId.CreateRequired("file"), CancellationToken.None);
            string temporaryPath;
            using (Stream stream = result.ContentStream)
            {
                FileStream file = Assert.IsType<FileStream>(stream);
                temporaryPath = file.Name;
                Assert.True(File.Exists(temporaryPath));
                Assert.True(file.CanSeek);
                Assert.Equal(6 * 1024 * 1024, file.Length);
                Assert.Equal(0, file.Position);
                file.Seek(-1, SeekOrigin.End);
                Assert.Equal(42, file.ReadByte());
            }
            Assert.False(File.Exists(temporaryPath));
            FileDownloadResult second = await service.DownloadFileAsync(DriveItemId.CreateRequired("file"), CancellationToken.None);
            using (Stream stream = second.ContentStream)
            {
                Assert.IsType<FileStream>(stream);
            }
        }
    }
}
