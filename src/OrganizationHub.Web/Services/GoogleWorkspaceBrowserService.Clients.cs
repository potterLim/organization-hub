using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceBrowserService
{
    private async Task<DriveService> createDriveServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(DRIVE_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new DriveService(initializer);
    }

    private async Task<CalendarService> createCalendarServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(CALENDAR_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new CalendarService(initializer);
    }

    private async Task<GoogleTasksApi> createTasksServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(TASK_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new GoogleTasksApi(initializer);
    }
}
