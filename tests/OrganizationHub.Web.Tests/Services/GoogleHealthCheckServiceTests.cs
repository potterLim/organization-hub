using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;
using GoogleCalendarApi = Google.Apis.Calendar.v3.CalendarService;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Tests.Services;

public sealed class GoogleHealthCheckServiceTests
{
    [Fact]
    public async Task GetHealthCheckItemsExcludesSystemAdministrationTeamResourcesAsync()
    {
        using (TestDbContextFactory dbContextFactory = createDbContextFactory())
        {
            GoogleHealthCheckService healthCheckService = new GoogleHealthCheckService(
                dbContextFactory,
                new ConnectedGoogleConnectionService(),
                new FakeDriveService(),
                new EmptyGoogleWorkspaceBrowserService());

            IReadOnlyList<GoogleHealthCheckItem> items = await healthCheckService.GetHealthCheckItemsAsync(CancellationToken.None);

            Assert.DoesNotContain(items, item => item.Name.Contains(OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME, StringComparison.Ordinal));
            Assert.Contains(items, item => item.Name == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME + " 자료");
            Assert.Contains(items, item => item.Name == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME);
            Assert.Contains(items, item => item.Name == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME + " Calendar");
            Assert.Contains(items, item => item.Name == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME + " Google Tasks 목록");
        }
    }

    private static TestDbContextFactory createDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.ID = 1;
            dbContext.GlobalSettings.Add(setting);
            GoogleStorageConnection connection = new GoogleStorageConnection();
            connection.ID = 1;
            connection.ConnectedEmail = "operations@example.com";
            connection.EncryptedRefreshToken = "encrypted-refresh-token";
            connection.Scopes = GoogleCalendarApi.Scope.Calendar + " " + GoogleTasksApi.Scope.Tasks;
            connection.IsActive = true;
            connection.ConnectedAt = DateTime.UtcNow;
            connection.UpdatedAt = DateTime.UtcNow;
            dbContext.GoogleStorageConnections.Add(connection);
            dbContext.Teams.Add(createTeam(1, OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY, OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME));
            dbContext.Teams.Add(createTeam(2, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME));
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static Team createTeam(int id, string teamKey, string displayName)
    {
        Team team = new Team();
        team.ID = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.IsActive = true;
        team.SortOrder = id;
        return team;
    }

    private sealed class ConnectedGoogleConnectionService : IGoogleConnectionService
    {
        public Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            GoogleConnectionStatus status = new GoogleConnectionStatus();
            status.HasOAuthConfiguration = true;
            status.IsConnected = true;
            status.ConnectedEmail = "operations@example.com";
            status.ConnectedAt = DateTime.UtcNow;
            return Task.FromResult(status);
        }

        public string CreateAuthorizationUrl(string redirectUri)
        {
            return string.Empty;
        }

        public Task<ServiceResult> CompleteConnectionAsync(string code, string state, string redirectUri, CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceResult.Succeed());
        }

        public Task<ServiceResult> DisconnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceResult.Succeed());
        }
    }

    private sealed class EmptyGoogleWorkspaceBrowserService : IGoogleWorkspaceBrowserService
    {
        public Task<IReadOnlyList<GoogleDriveFolderItem>> ListDriveFoldersAsync(string parentFolderID, CancellationToken cancellationToken)
        {
            IReadOnlyList<GoogleDriveFolderItem> folders = new List<GoogleDriveFolderItem>();
            return Task.FromResult(folders);
        }

        public Task<IReadOnlyList<GoogleCalendarListItem>> ListCalendarsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<GoogleCalendarListItem> calendars = new List<GoogleCalendarListItem>();
            return Task.FromResult(calendars);
        }

        public Task<IReadOnlyList<GoogleTaskListItem>> ListTaskListsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<GoogleTaskListItem> taskLists = new List<GoogleTaskListItem>();
            return Task.FromResult(taskLists);
        }
    }
}
