using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleHealthCheckService : IGoogleHealthCheckService
{
    private const string STATUS_HEALTHY = "Healthy";
    private const string STATUS_WARNING = "Warning";
    private const string STATUS_ERROR = "Error";

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IGoogleConnectionService mGoogleConnectionService;
    private readonly IDriveService mDriveService;
    private readonly IGoogleWorkspaceBrowserService mGoogleWorkspaceBrowserService;

    public GoogleHealthCheckService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IGoogleConnectionService googleConnectionService,
        IDriveService driveService,
        IGoogleWorkspaceBrowserService googleWorkspaceBrowserService)
    {
        mDbContextFactory = dbContextFactory;
        mGoogleConnectionService = googleConnectionService;
        mDriveService = driveService;
        mGoogleWorkspaceBrowserService = googleWorkspaceBrowserService;
    }

    public async Task<IReadOnlyList<GoogleHealthCheckItem>> GetHealthCheckItemsAsync(CancellationToken cancellationToken)
    {
        List<GoogleHealthCheckItem> items = new List<GoogleHealthCheckItem>();
        GoogleConnectionStatus status = await mGoogleConnectionService.GetStatusAsync(cancellationToken);
        addItem(
            items,
            "연결",
            "Google 연결 정보",
            status.HasOAuthConfiguration ? STATUS_HEALTHY : STATUS_ERROR,
            status.HasOAuthConfiguration ? "Google 연결 정보가 준비되어 있습니다." : "배포 설정에 Google 연결 정보가 필요합니다.");
        addItem(
            items,
            "연결",
            "Google 계정",
            status.IsConnected ? STATUS_HEALTHY : STATUS_ERROR,
            status.IsConnected ? status.ConnectedEmail + " 계정이 연결되어 있습니다." : "조직 운영용 Google 계정을 연결해 주세요.");
        if (status.IsConnected == false)
        {
            return items;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GoogleStorageConnection? connectionOrNull = await dbContext.GoogleStorageConnections
                .Where(connection => connection.IsActive)
                .OrderByDescending(connection => connection.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            addCalendarScopeItem(items, connectionOrNull);
            addTasksScopeItem(items, connectionOrNull);
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings
                .OrderBy(setting => setting.Id)
                .FirstOrDefaultAsync(cancellationToken);
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            if (settingOrNull == null)
            {
                addItem(items, "설정", "시스템 설정", STATUS_ERROR, "시스템 설정 행이 없습니다.");
                return items;
            }

            try
            {
                await addFolderItemsAsync(items, settingOrNull, teams, cancellationToken);
            }
            catch (Exception exception)
            {
                addItem(items, "Drive", "폴더 접근", STATUS_ERROR, "Drive 폴더 진단 중 오류가 발생했습니다. " + exception.Message);
            }

            try
            {
                await addCalendarItemsAsync(items, settingOrNull, teams, cancellationToken);
            }
            catch (Exception exception)
            {
                addItem(items, "Calendar", "Calendar 접근", STATUS_ERROR, "Calendar 진단 중 오류가 발생했습니다. " + exception.Message);
            }

            try
            {
                await addTaskListItemsAsync(items, settingOrNull, teams, cancellationToken);
            }
            catch (Exception exception)
            {
                addItem(items, "Google Tasks", "Google Tasks 목록 접근", STATUS_ERROR, createGoogleTasksErrorMessage(exception));
            }

            try
            {
                await addFileItemsAsync(items, settingOrNull, cancellationToken);
            }
            catch (Exception exception)
            {
                addItem(items, "파일", "공지/업무 기준 파일", STATUS_ERROR, "연결 파일 진단 중 오류가 발생했습니다. " + exception.Message);
            }
        }

        return items;
    }

    private async Task<List<Team>> getWorkspaceTeamsAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Teams
            .Where(team => team.IsActive && team.TeamKey != OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY)
            .OrderBy(team => team.SortOrder)
            .ThenBy(team => team.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private void addItem(List<GoogleHealthCheckItem> items, string area, string name, string status, string message)
    {
        GoogleHealthCheckItem item = new GoogleHealthCheckItem();
        item.Area = area;
        item.Name = name;
        item.Status = status;
        item.Message = message;
        items.Add(item);
    }

    private string createGoogleTasksErrorMessage(Exception exception)
    {
        return GoogleApiExceptionMessageFormatter.CreateGoogleTasksMessage(exception, "Google Tasks 진단 중 오류가 발생했습니다.");
    }

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }
}
