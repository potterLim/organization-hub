using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
    private async Task<DriveService> createDriveServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(DRIVE_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new DriveService(initializer);
    }
}
