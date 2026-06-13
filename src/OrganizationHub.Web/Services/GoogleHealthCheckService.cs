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
using GoogleCalendarApi = Google.Apis.Calendar.v3.CalendarService;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed class GoogleHealthCheckService : IGoogleHealthCheckService
{
    private const string STATUS_HEALTHY = "Healthy";
    private const string STATUS_WARNING = "Warning";
    private const string STATUS_ERROR = "Error";

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IGoogleConnectionService mGoogleConnectionService;
    private readonly IDriveService mDriveService;
    private readonly IGoogleWorkspaceBrowserService mGoogleWorkspaceBrowserService;

    public GoogleHealthCheckService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IGoogleConnectionService googleConnectionService, IDriveService driveService, IGoogleWorkspaceBrowserService googleWorkspaceBrowserService)
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
        addItem(items, "연결", "Google 연결 정보", status.HasOAuthConfiguration ? STATUS_HEALTHY : STATUS_ERROR, status.HasOAuthConfiguration ? "Google 연결 정보가 준비되어 있습니다." : "배포 설정에 Google 연결 정보가 필요합니다.");
        addItem(items, "연결", "Google 계정", status.IsConnected ? STATUS_HEALTHY : STATUS_ERROR, status.IsConnected ? status.ConnectedEmail + " 계정이 연결되어 있습니다." : "조직 운영용 Google 계정을 연결해 주세요.");
        if (status.IsConnected == false)
        {
            return items;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GoogleStorageConnection? connectionOrNull = await dbContext.GoogleStorageConnections.Where(connection => connection.IsActive).OrderByDescending(connection => connection.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
            addCalendarScopeItem(items, connectionOrNull);
            addTasksScopeItem(items, connectionOrNull);
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
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
                addItem(items, "파일", "공지/운영 규칙 파일", STATUS_ERROR, "연결 파일 진단 중 오류가 발생했습니다. " + exception.Message);
            }
        }

        return items;
    }

    private void addCalendarScopeItem(List<GoogleHealthCheckItem> items, GoogleStorageConnection? connectionOrNull)
    {
        if (connectionOrNull == null)
        {
            addItem(items, "연결", "Calendar 수정 권한", STATUS_ERROR, "활성 Google 연결을 찾을 수 없습니다.");
            return;
        }

        bool hasCalendarWriteScope = connectionOrNull.Scopes.Contains(GoogleCalendarApi.Scope.Calendar);
        addItem(items, "연결", "Calendar 수정 권한", hasCalendarWriteScope ? STATUS_HEALTHY : STATUS_WARNING, hasCalendarWriteScope ? "일정 추가, 수정, 삭제 권한이 포함되어 있습니다." : "기존 연결은 읽기만 가능할 수 있습니다. Google 계정을 다시 연결해 주세요.");
    }

    private void addTasksScopeItem(List<GoogleHealthCheckItem> items, GoogleStorageConnection? connectionOrNull)
    {
        if (connectionOrNull == null)
        {
            addItem(items, "연결", "Google Tasks 권한", STATUS_ERROR, "활성 Google 연결을 찾을 수 없습니다.");
            return;
        }

        bool hasTasksScope = connectionOrNull.Scopes.Contains(GoogleTasksApi.Scope.Tasks);
        addItem(items, "연결", "Google Tasks 권한", hasTasksScope ? STATUS_HEALTHY : STATUS_WARNING, hasTasksScope ? "할 일 추가, 수정, 삭제 권한이 포함되어 있습니다." : "기존 연결에 Google Tasks 권한이 없습니다. Google 계정을 다시 연결해 주세요.");
    }

    private async Task addFolderItemsAsync(List<GoogleHealthCheckItem> items, GlobalSetting setting, List<Team> teams, CancellationToken cancellationToken)
    {
        await addFolderItemAsync(items, "Drive", "기본 자료 위치", setting.OfficialRootFolderID, string.Empty, EGoogleHealthFolderCheckKind.RootFolder, cancellationToken);
        string officialRootFolderID = getNullAsEmpty(setting.OfficialRootFolderID);
        await addFolderItemAsync(items, "Drive", OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, setting.PublicFolderID, officialRootFolderID, EGoogleHealthFolderCheckKind.ChildFolder, cancellationToken);
        await addFolderItemAsync(items, "Drive", OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, setting.CommonFolderID, officialRootFolderID, EGoogleHealthFolderCheckKind.ChildFolder, cancellationToken);
        await addFolderItemAsync(items, "Drive", OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, setting.ResponsibleResourcesFolderID, officialRootFolderID, EGoogleHealthFolderCheckKind.ChildFolder, cancellationToken);
        await addFolderItemAsync(items, "Drive", OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, setting.WorkResourcesFolderID, officialRootFolderID, EGoogleHealthFolderCheckKind.ChildFolder, cancellationToken);
        foreach (Team team in teams)
        {
            await addFolderItemAsync(items, "팀 Drive", team.DisplayName + " 자료", team.MaterialsFolderID, officialRootFolderID, EGoogleHealthFolderCheckKind.ChildFolder, cancellationToken);
            await addFolderItemAsync(items, "팀 Drive", team.DisplayName + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, team.ApprovedFolderID, officialRootFolderID, EGoogleHealthFolderCheckKind.ChildFolder, cancellationToken);
        }
    }

    private async Task<List<Team>> getWorkspaceTeamsAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Teams
            .Where(team => team.IsActive && team.TeamKey != OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY)
            .OrderBy(team => team.SortOrder)
            .ThenBy(team => team.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private async Task addFolderItemAsync(List<GoogleHealthCheckItem> items, string area, string name, string? folderIDOrNull, string officialRootFolderID, EGoogleHealthFolderCheckKind folderCheckKind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderIDOrNull))
        {
            addItem(items, area, name, STATUS_WARNING, "폴더가 연결되지 않았습니다.");
            return;
        }

        DriveFileMetadata? metadataOrNull = await mDriveService.GetFileOrNullAsync(folderIDOrNull, cancellationToken);
        if (metadataOrNull == null)
        {
            addItem(items, area, name, STATUS_ERROR, "연결된 계정에서 폴더를 찾을 수 없습니다.");
            return;
        }

        if (folderCheckKind == EGoogleHealthFolderCheckKind.RootFolder || string.IsNullOrWhiteSpace(officialRootFolderID))
        {
            addItem(items, area, name, STATUS_HEALTHY, metadataOrNull.Name + " 폴더에 접근할 수 있습니다.");
            return;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderIDOrNull, cancellationToken);
        addItem(items, area, name, ancestorFolderIDs.Contains(officialRootFolderID) ? STATUS_HEALTHY : STATUS_ERROR, ancestorFolderIDs.Contains(officialRootFolderID) ? metadataOrNull.Name + " 폴더가 기본 자료 위치 안에 있습니다." : metadataOrNull.Name + " 폴더가 기본 자료 위치 안에 있지 않습니다.");
    }

    private async Task addCalendarItemsAsync(List<GoogleHealthCheckItem> items, GlobalSetting setting, List<Team> teams, CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleCalendarListItem> calendars = await mGoogleWorkspaceBrowserService.ListCalendarsAsync(cancellationToken);
        HashSet<string> calendarIDs = new HashSet<string>(calendars.Select(calendar => calendar.ID));
        addCalendarItem(items, "Calendar", OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME + " Calendar", setting.OrganizationCalendarID, calendarIDs);
        addCalendarItem(items, "Calendar", OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME + " Calendar", setting.ResponsibleCalendarID, calendarIDs);
        foreach (Team team in teams)
        {
            addCalendarItem(items, "팀 Calendar", team.DisplayName + " Calendar", team.CalendarID, calendarIDs);
        }
    }

    private void addCalendarItem(List<GoogleHealthCheckItem> items, string area, string name, string? calendarIDOrNull, HashSet<string> calendarIDs)
    {
        if (string.IsNullOrWhiteSpace(calendarIDOrNull))
        {
            addItem(items, area, name, STATUS_WARNING, "Calendar가 연결되지 않았습니다.");
            return;
        }

        addItem(items, area, name, calendarIDs.Contains(calendarIDOrNull) ? STATUS_HEALTHY : STATUS_ERROR, calendarIDs.Contains(calendarIDOrNull) ? "연결된 계정에서 확인되었습니다." : "연결된 계정에서 이 Calendar를 찾을 수 없습니다.");
    }

    private async Task addTaskListItemsAsync(List<GoogleHealthCheckItem> items, GlobalSetting setting, List<Team> teams, CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleTaskListItem> taskLists = await mGoogleWorkspaceBrowserService.ListTaskListsAsync(cancellationToken);
        HashSet<string> taskListIDs = new HashSet<string>(taskLists.Select(taskList => taskList.ID));
        addTaskListItem(items, "Google Tasks", "조직 공용 Google Tasks 목록", setting.OrganizationTaskListID, taskListIDs);
        addTaskListItem(items, "Google Tasks", "책임자 Google Tasks 목록", setting.ResponsibleTaskListID, taskListIDs);
        foreach (Team team in teams)
        {
            addTaskListItem(items, "팀 Google Tasks", team.DisplayName + " Google Tasks 목록", team.TaskListID, taskListIDs);
        }
    }

    private void addTaskListItem(List<GoogleHealthCheckItem> items, string area, string name, string? taskListIDOrNull, HashSet<string> taskListIDs)
    {
        if (string.IsNullOrWhiteSpace(taskListIDOrNull))
        {
            addItem(items, area, name, STATUS_WARNING, "Google Tasks 목록이 연결되지 않았습니다.");
            return;
        }

        addItem(items, area, name, taskListIDs.Contains(taskListIDOrNull) ? STATUS_HEALTHY : STATUS_ERROR, taskListIDs.Contains(taskListIDOrNull) ? "연결된 계정에서 확인되었습니다." : "연결된 계정에서 이 Google Tasks 목록을 찾을 수 없습니다.");
    }

    private async Task addFileItemsAsync(List<GoogleHealthCheckItem> items, GlobalSetting setting, CancellationToken cancellationToken)
    {
        await addFileItemAsync(items, "파일", "공지 파일", setting.NoticeFileID, cancellationToken);
        await addFileItemAsync(items, "파일", "운영 규칙 파일", setting.WorkGuidelineFileID, cancellationToken);
    }

    private async Task addFileItemAsync(List<GoogleHealthCheckItem> items, string area, string name, string? fileIDOrNull, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileIDOrNull))
        {
            addItem(items, area, name, STATUS_WARNING, "파일이 연결되지 않았습니다.");
            return;
        }

        DriveFileMetadata? metadataOrNull = await mDriveService.GetFileOrNullAsync(fileIDOrNull, cancellationToken);
        addItem(items, area, name, metadataOrNull == null ? STATUS_ERROR : STATUS_HEALTHY, metadataOrNull == null ? "연결된 계정에서 파일을 찾을 수 없습니다." : metadataOrNull.Name + " 파일에 접근할 수 있습니다.");
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

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }
}
