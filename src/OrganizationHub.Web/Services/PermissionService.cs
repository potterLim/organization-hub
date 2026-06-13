using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService : IPermissionService
{
    private const string GOOGLE_DRIVE_FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IDriveService mDriveService;
    private readonly IMemoryCache mMemoryCache;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public PermissionService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IDriveService driveService, IMemoryCache memoryCache)
        : this(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory))
    {
    }

    public PermissionService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IDriveService driveService, IMemoryCache memoryCache, IOrganizationPermissionService organizationPermissionService)
    {
        mDbContextFactory = dbContextFactory;
        mDriveService = driveService;
        mMemoryCache = memoryCache;
        mOrganizationPermissionService = organizationPermissionService;
    }

    public async Task<IReadOnlyList<DriveFolderReference>> GetReadableDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, null, cancellationToken);
        return folders;
    }

    public async Task<IReadOnlyList<DriveFolderReference>> GetReadableDriveFoldersAsync(UserSession userSession, EDriveFolderKind folderKind, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, folderKind, cancellationToken);
        return folders;
    }

    public async Task<IReadOnlyList<DriveFolderReference>> GetUploadableDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Uploadable, null, cancellationToken);
        return folders;
    }

    public async Task<IReadOnlyList<DriveFolderReference>> GetApprovalDestinationDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, EDriveFolderKind.TeamApproved, cancellationToken);
        if (UserPermissionPolicy.CanUseOnlyManagementApprovalFolders(userSession.Role))
        {
            folders = folders.Where(isManagementApprovedFolder).ToList();
        }

        List<DriveFolderReference> destinationFolders = folders.Where(folder => folder.FolderKind == EDriveFolderKind.TeamApproved).OrderBy(folder => getApprovalDestinationSortOrder(folder)).ThenBy(folder => folder.TeamName).ThenBy(folder => folder.DisplayName).ToList();
        return destinationFolders;
    }

    public async Task<IReadOnlyList<CalendarReference>> GetReadableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        List<CalendarReference> calendars = new List<CalendarReference>();
        calendars.Add(new CalendarReference("all", "전체", ECalendarViewKind.Aggregate, null));
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            bool canReadGlobalCalendars = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.CALENDAR_READ, null, usesConfiguredPermissions, userSession.IsManagementRole(), cancellationToken);
            if (canReadGlobalCalendars && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.OrganizationCalendarID, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            bool canReadResponsibleCalendar = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.CALENDAR_READ, null, usesConfiguredPermissions, userSession.IsTeamLeaderOrHigherRole(), cancellationToken);
            if (canReadResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.ResponsibleCalendarID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarID != null && team.CalendarID != string.Empty).OrderBy(team => team.SortOrder);
            if (usesConfiguredPermissions == false && userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    return calendars;
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarID))
                {
                    continue;
                }

                bool canReadTeamCalendar = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.CALENDAR_READ, team.ID, usesConfiguredPermissions, canReadMaterialsForTeam(userSession, team), cancellationToken);
                if (canReadTeamCalendar)
                {
                    calendars.Add(new CalendarReference(team.CalendarID, team.DisplayName, ECalendarViewKind.Team, team.ID));
                }
            }
        }

        return calendars;
    }

    public async Task<IReadOnlyList<CalendarReference>> GetWritableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        List<CalendarReference> calendars = new List<CalendarReference>();
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            bool canWriteManagementCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(userSession, null, usesConfiguredPermissions, userSession.IsManagementRole(), cancellationToken);
            if (canWriteManagementCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.OrganizationCalendarID, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            bool canWriteResponsibleCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(userSession, null, usesConfiguredPermissions, UserPermissionPolicy.CanWriteResponsibleWork(userSession.Role), cancellationToken);
            if (canWriteResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.ResponsibleCalendarID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarID != null && team.CalendarID != string.Empty).OrderBy(team => team.SortOrder);
            if (usesConfiguredPermissions == false && UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role) == false)
            {
                if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
                {
                    return calendars;
                }

                if (userSession.TeamID.HasValue == false)
                {
                    return calendars;
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarID))
                {
                    continue;
                }

                bool canWriteTeamCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(userSession, team.ID, usesConfiguredPermissions, canWriteTeamWorkFallback(userSession, team), cancellationToken);
                if (canWriteTeamCalendar)
                {
                    calendars.Add(new CalendarReference(team.CalendarID, team.DisplayName, ECalendarViewKind.Team, team.ID));
                }
            }
        }

        return calendars;
    }

    public async Task<bool> CanReadFileAsync(UserSession userSession, string fileID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideReadFileAsync(userSession, fileID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideReadFileAsync(UserSession userSession, string fileID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileID))
        {
            return PermissionDecision.Deny("파일을 찾을 수 없습니다.", "자료 파일");
        }

        string officialRootFolderID = string.Empty;
        bool isConfiguredSpecialFile = false;
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderID = getNullAsEmpty(settingOrNull.OfficialRootFolderID);
                if (settingOrNull.NoticeFileID == fileID || settingOrNull.WorkGuidelineFileID == fileID)
                {
                    isConfiguredSpecialFile = true;
                }
            }
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(fileID, cancellationToken);
        if (isInsideOfficialRoot(ancestorFolderIDs, officialRootFolderID) == false)
        {
            return PermissionDecision.Deny("공식 자료 영역 밖의 파일입니다.", "자료 파일");
        }

        if (isConfiguredSpecialFile)
        {
            return PermissionDecision.Allow("공지/운영 규칙");
        }

        IReadOnlyList<DriveFolderReference> readableFolders = await GetReadableDriveFoldersAsync(userSession, cancellationToken);
        foreach (DriveFolderReference folder in readableFolders)
        {
            if (ancestorFolderIDs.Contains(folder.FolderID))
            {
                return PermissionDecision.Allow(folder.DisplayName);
            }
        }

        bool hasApprovalFileAccess = await canReadApprovalFileAsync(userSession, fileID, cancellationToken);
        if (hasApprovalFileAccess)
        {
            return PermissionDecision.Allow("결재 관련 자료");
        }

        return PermissionDecision.Deny("이 파일을 볼 수 있는 자료 영역이나 결재 기록이 없습니다.", "자료 파일");
    }

    public async Task<bool> CanWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideWriteCalendarAsync(userSession, calendarID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        IReadOnlyList<CalendarReference> calendars = await GetWritableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference calendar in calendars)
        {
            if (calendar.CalendarID == calendarID)
            {
                return PermissionDecision.Allow(calendar.DisplayName);
            }
        }

        IReadOnlyList<CalendarReference> readableCalendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        CalendarReference? readableCalendarOrNull = readableCalendars.FirstOrDefault(calendar => calendar.CalendarID == calendarID);
        if (readableCalendarOrNull != null)
        {
            return PermissionDecision.Deny("조회만 가능한 일정입니다.", readableCalendarOrNull.DisplayName);
        }

        return PermissionDecision.Deny("접근할 수 없는 일정입니다.", "일정");
    }

    public async Task<bool> CanUploadToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideUploadToFolderAsync(userSession, folderID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideUploadToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        DriveFolderReference? folderOrNull = await getAllowedFolderRootForFolderAsync(userSession, folderID, EDriveFolderRootSelection.Uploadable, cancellationToken);
        if (folderOrNull == null)
        {
            return PermissionDecision.Deny("선택한 위치에 파일을 올릴 권한이 없습니다.", "파일 올리기");
        }

        return PermissionDecision.Allow(folderOrNull.DisplayName);
    }

    public async Task<bool> CanCreateFolderInFolderAsync(UserSession userSession, string parentFolderID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideCreateFolderInFolderAsync(userSession, parentFolderID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideCreateFolderInFolderAsync(UserSession userSession, string parentFolderID, CancellationToken cancellationToken)
    {
        PermissionDecision uploadDecision = await DecideUploadToFolderAsync(userSession, parentFolderID, cancellationToken);
        if (uploadDecision.IsAllowed)
        {
            return PermissionDecision.Allow(uploadDecision.ScopeLabel);
        }

        return PermissionDecision.Deny("이 위치에 폴더를 만들 권한이 없습니다.", uploadDecision.ScopeLabel);
    }

    public async Task<bool> CanDeleteInFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideDeleteInFolderAsync(userSession, folderID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideDeleteInFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        DriveFolderReference? rootFolderOrNull = await getReadableFolderRootForFolderAsync(userSession, folderID, cancellationToken);
        if (rootFolderOrNull == null)
        {
            return PermissionDecision.Deny("이 위치의 파일을 삭제할 권한이 없습니다.", "자료 삭제");
        }

        return await decideDeleteInsideRootAsync(userSession, rootFolderOrNull, cancellationToken);
    }

    public bool CanDeleteInsideRoot(UserSession userSession, DriveFolderReference rootFolder)
    {
        return DecideDeleteInsideRoot(userSession, rootFolder).IsAllowed;
    }

    public PermissionDecision DecideDeleteInsideRoot(UserSession userSession, DriveFolderReference rootFolder)
    {
        return UserPermissionPolicy.DecideMaterialDelete(userSession, rootFolder);
    }

    private async Task<PermissionDecision> decideDeleteInsideRootAsync(UserSession userSession, DriveFolderReference rootFolder, CancellationToken cancellationToken)
    {
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        if (usesConfiguredPermissions == false)
        {
            return DecideDeleteInsideRoot(userSession, rootFolder);
        }

        PermissionDecision permissionDecision = await mOrganizationPermissionService.DecidePermissionAsync(userSession, PermissionKeys.FILES_DELETE, rootFolder.TeamID, cancellationToken);
        if (permissionDecision.IsAllowed)
        {
            return PermissionDecision.Allow(rootFolder.DisplayName);
        }

        return PermissionDecision.Deny("이 위치의 파일을 삭제할 권한이 없습니다.", rootFolder.DisplayName);
    }

    public async Task<bool> CanStoreApprovedFileToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideStoreApprovedFileToFolderAsync(userSession, folderID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideStoreApprovedFileToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        DriveFolderReference? folderOrNull = await getAllowedFolderRootForFolderAsync(userSession, folderID, EDriveFolderRootSelection.ApprovalDestination, cancellationToken);
        if (folderOrNull == null)
        {
            return PermissionDecision.Deny("이 위치에 승인본을 저장할 권한이 없습니다.", "승인본 저장");
        }

        return PermissionDecision.Allow(folderOrNull.DisplayName);
    }

    public async Task<bool> CanReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideReadCalendarAsync(userSession, calendarID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        if (calendarID == "all")
        {
            return PermissionDecision.Allow("전체 일정");
        }

        IReadOnlyList<CalendarReference> calendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference calendar in calendars)
        {
            if (calendar.CalendarID == calendarID)
            {
                return PermissionDecision.Allow(calendar.DisplayName);
            }
        }

        return PermissionDecision.Deny("접근할 수 없는 일정입니다.", "일정");
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
