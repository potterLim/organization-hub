using System;
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

public sealed class PermissionService : IPermissionService
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

    private async Task<IReadOnlyList<DriveFolderReference>> getDriveFoldersAsync(UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, CancellationToken cancellationToken)
    {
        List<DriveFolderReference> folders = new List<DriveFolderReference>();
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                await addGlobalFoldersAsync(folders, settingOrNull, userSession, accessMode, folderKindOrNull, usesConfiguredPermissions, cancellationToken);
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder);
            if (usesConfiguredPermissions == false && userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    IReadOnlyList<DriveFolderReference> filteredTeamlessFolders = filterFolders(folders, accessMode);
                    IReadOnlyList<DriveFolderReference> allowedTeamlessFolders = await filterFoldersInsideOfficialRootAsync(filteredTeamlessFolders, cancellationToken);
                    return deduplicateFolderReferences(allowedTeamlessFolders);
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                await addTeamFoldersAsync(folders, team, userSession, accessMode, folderKindOrNull, usesConfiguredPermissions, cancellationToken);
            }
        }

        IReadOnlyList<DriveFolderReference> filteredFolders = filterFolders(folders, accessMode);
        IReadOnlyList<DriveFolderReference> allowedFolders = await filterFoldersInsideOfficialRootAsync(filteredFolders, cancellationToken);
        return deduplicateFolderReferences(allowedFolders);
    }

    private async Task addGlobalFoldersAsync(List<DriveFolderReference> folders, GlobalSetting setting, UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        bool canReadGlobalFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_READ, null, usesConfiguredPermissions, true, cancellationToken);
        bool canUploadPublic = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadPublicMaterials(userSession.Role), cancellationToken);
        bool canUploadCommon = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadCommonMaterials(userSession.Role), cancellationToken);
        bool canUploadWorkResources = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadWorkResources(userSession.Role), cancellationToken);
        addFolderIfPermitted(folders, setting.PublicFolderID, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, EDriveFolderKind.Public, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadPublic, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, setting.CommonFolderID, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, EDriveFolderKind.Common, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadCommon, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, setting.WorkResourcesFolderID, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, EDriveFolderKind.WorkResources, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadWorkResources, accessMode, folderKindOrNull);
        bool canReadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_READ, null, usesConfiguredPermissions, userSession.IsTeamLeaderOrHigherRole(), cancellationToken);
        if (canReadResponsibleFolder)
        {
            bool canUploadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadResponsibleResources(userSession.Role), cancellationToken);
            addFolderIfPermitted(folders, setting.ResponsibleResourcesFolderID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, EDriveFolderKind.ResponsibleResources, null, string.Empty, string.Empty, canReadResponsibleFolder, canUploadResponsibleFolder, accessMode, folderKindOrNull);
        }
    }

    private async Task addTeamFoldersAsync(List<DriveFolderReference> folders, Team team, UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        bool canReadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_READ, team.ID, usesConfiguredPermissions, canReadMaterialsForTeam(userSession, team), cancellationToken);
        bool canUploadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, team.ID, usesConfiguredPermissions, canUploadMaterialsForTeam(userSession, team), cancellationToken);
        addFolderIfPermitted(folders, team.MaterialsFolderID, team.DisplayName + " 자료", EDriveFolderKind.TeamMaterials, team.ID, team.TeamKey, team.DisplayName, canReadTeamFiles, canUploadTeamFiles, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, team.ApprovedFolderID, team.DisplayName + " 결재 완료 자료", EDriveFolderKind.TeamApproved, team.ID, team.TeamKey, team.DisplayName, canReadTeamFiles, false, accessMode, folderKindOrNull);
    }

    private async Task<DriveFolderReference?> getAllowedFolderRootForFolderAsync(UserSession userSession, string folderID, EDriveFolderRootSelection rootSelection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderID))
        {
            return null;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
        {
            return null;
        }

        IReadOnlyList<DriveFolderReference> candidateFolders;
        if (rootSelection == EDriveFolderRootSelection.ApprovalDestination)
        {
            candidateFolders = await GetApprovalDestinationDriveFoldersAsync(userSession, cancellationToken);
        }
        else
        {
            EDriveFolderAccessMode accessMode = rootSelection == EDriveFolderRootSelection.Uploadable ? EDriveFolderAccessMode.Uploadable : EDriveFolderAccessMode.Readable;
            candidateFolders = await getDriveFoldersAsync(userSession, accessMode, null, cancellationToken);
        }

        if (candidateFolders.Count == 0)
        {
            return null;
        }

        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (candidateFolder.FolderID == folderID)
            {
                bool canUseRootFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
                if (canUseRootFolder)
                {
                    return candidateFolder;
                }

                return null;
            }
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (ancestorFolderIDs.Contains(candidateFolder.FolderID) == false)
            {
                continue;
            }

            bool canUseFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
            if (canUseFolder)
            {
                return candidateFolder;
            }
        }

        return null;
    }

    private async Task<DriveFolderReference?> getReadableFolderRootForFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderID))
        {
            return null;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
        {
            return null;
        }

        IReadOnlyList<DriveFolderReference> candidateFolders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, null, cancellationToken);
        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (candidateFolder.FolderID == folderID)
            {
                bool canUseRootFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
                return canUseRootFolder ? candidateFolder : null;
            }
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (ancestorFolderIDs.Contains(candidateFolder.FolderID) == false)
            {
                continue;
            }

            bool canUseFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
            if (canUseFolder)
            {
                return candidateFolder;
            }
        }

        return null;
    }

    private void addFolderIfConfigured(List<DriveFolderReference> folders, string? folderIDOrNull, string displayName, EDriveFolderKind folderKind, int? teamID, string teamKey, string teamName, EDriveFolderUploadAccess uploadAccess, EDriveFolderKind? folderKindOrNull)
    {
        if (string.IsNullOrWhiteSpace(folderIDOrNull))
        {
            return;
        }

        if (folderKindOrNull.HasValue && folderKindOrNull.Value != folderKind)
        {
            return;
        }

        folders.Add(new DriveFolderReference(folderIDOrNull, displayName, folderKind, teamID, teamKey, teamName, uploadAccess));
    }

    private void addFolderIfPermitted(List<DriveFolderReference> folders, string? folderIDOrNull, string displayName, EDriveFolderKind folderKind, int? teamIDOrNull, string teamKey, string teamName, bool canRead, bool canUpload, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull)
    {
        if (accessMode == EDriveFolderAccessMode.Uploadable && canUpload == false)
        {
            return;
        }

        if (accessMode != EDriveFolderAccessMode.Uploadable && canRead == false && canUpload == false)
        {
            return;
        }

        EDriveFolderUploadAccess uploadAccess = canUpload ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
        addFolderIfConfigured(folders, folderIDOrNull, displayName, folderKind, teamIDOrNull, teamKey, teamName, uploadAccess, folderKindOrNull);
    }

    private async Task<bool> canUseConfiguredOrFallbackPermissionAsync(UserSession userSession, string permissionKey, int? targetTeamIDOrNull, bool usesConfiguredPermissions, bool fallbackPermission, CancellationToken cancellationToken)
    {
        if (usesConfiguredPermissions)
        {
            return await mOrganizationPermissionService.HasPermissionAsync(userSession, permissionKey, targetTeamIDOrNull, cancellationToken);
        }

        return fallbackPermission;
    }

    private async Task<bool> canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(UserSession userSession, int? targetTeamIDOrNull, bool usesConfiguredPermissions, bool fallbackPermission, CancellationToken cancellationToken)
    {
        if (usesConfiguredPermissions == false)
        {
            return fallbackPermission;
        }

        bool canCreate = await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.CALENDAR_CREATE, targetTeamIDOrNull, cancellationToken);
        if (canCreate)
        {
            return true;
        }

        bool canUpdate = await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.CALENDAR_UPDATE, targetTeamIDOrNull, cancellationToken);
        if (canUpdate)
        {
            return true;
        }

        return await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.CALENDAR_DELETE, targetTeamIDOrNull, cancellationToken);
    }

    private IReadOnlyList<DriveFolderReference> filterFolders(List<DriveFolderReference> folders, EDriveFolderAccessMode accessMode)
    {
        if (accessMode != EDriveFolderAccessMode.Uploadable)
        {
            return folders;
        }

        List<DriveFolderReference> uploadableFolders = folders.Where(folder => folder.CanUpload).ToList();
        return uploadableFolders;
    }

    private static IReadOnlyList<DriveFolderReference> deduplicateFolderReferences(IReadOnlyList<DriveFolderReference> folders)
    {
        List<DriveFolderReference> deduplicatedFolders = new List<DriveFolderReference>();
        foreach (IGrouping<string, DriveFolderReference> folderGroup in folders.GroupBy(folder => folder.FolderID))
        {
            DriveFolderReference selectedFolder = folderGroup.OrderBy(getFolderReferencePriority).ThenByDescending(folder => folder.CanUpload).ThenBy(folder => folder.DisplayName).First();
            deduplicatedFolders.Add(selectedFolder);
        }

        return deduplicatedFolders;
    }

    private static int getFolderReferencePriority(DriveFolderReference folder)
    {
        switch (folder.FolderKind)
        {
            case EDriveFolderKind.Public:
            case EDriveFolderKind.Common:
            case EDriveFolderKind.WorkResources:
            case EDriveFolderKind.ResponsibleResources:
                return 0;
            case EDriveFolderKind.TeamMaterials:
                return 10;
            case EDriveFolderKind.TeamApproved:
                return 20;
            default:
                return 100;
        }
    }

    private int getApprovalDestinationSortOrder(DriveFolderReference folder)
    {
        if (folder.IsManagementTeam)
        {
            return 0;
        }

        return 1;
    }

    private bool isManagementApprovedFolder(DriveFolderReference folder)
    {
        if (folder.FolderKind != EDriveFolderKind.TeamApproved)
        {
            return false;
        }

        return folder.IsManagementTeam;
    }

    private bool canUploadMaterialsForTeam(UserSession userSession, Team team)
    {
        if (isManagementTeam(team))
        {
            return UserPermissionPolicy.CanUploadManagementMaterials(userSession.Role);
        }

        return UserPermissionPolicy.CanUploadTeamMaterials(userSession.Role);
    }

    private bool canReadMaterialsForTeam(UserSession userSession, Team team)
    {
        if (userSession.IsManagementRole())
        {
            return true;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        return userSession.TeamID.Value == team.ID;
    }

    private bool canWriteTeamWorkFallback(UserSession userSession, Team team)
    {
        if (UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role))
        {
            return true;
        }

        if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
        {
            return false;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        return userSession.TeamID.Value == team.ID;
    }

    private bool isManagementTeam(Team team)
    {
        return string.Equals(team.TeamKey, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> canUseFolderInsideOfficialRootAsync(string folderID, CancellationToken cancellationToken)
    {
        string officialRootFolderID = string.Empty;
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderID = getNullAsEmpty(settingOrNull.OfficialRootFolderID);
            }
        }

        if (string.IsNullOrWhiteSpace(officialRootFolderID))
        {
            return false;
        }

        if (folderID == officialRootFolderID)
        {
            return true;
        }

        return await isFolderInsideOfficialRootAsync(folderID, officialRootFolderID, cancellationToken);
    }

    private async Task<IReadOnlyList<DriveFolderReference>> filterFoldersInsideOfficialRootAsync(IReadOnlyList<DriveFolderReference> folders, CancellationToken cancellationToken)
    {
        string officialRootFolderID = string.Empty;
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderID = getNullAsEmpty(settingOrNull.OfficialRootFolderID);
            }
        }

        if (string.IsNullOrWhiteSpace(officialRootFolderID))
        {
            return new List<DriveFolderReference>();
        }

        List<Task<DriveFolderFilterResult>> filterTasks = new List<Task<DriveFolderFilterResult>>();
        foreach (DriveFolderReference folder in folders)
        {
            filterTasks.Add(createDriveFolderFilterResultAsync(folder, officialRootFolderID, cancellationToken));
        }

        DriveFolderFilterResult[] filterResults = await Task.WhenAll(filterTasks);
        List<DriveFolderReference> allowedFolders = new List<DriveFolderReference>();
        foreach (DriveFolderFilterResult filterResult in filterResults)
        {
            if (filterResult.IsAllowed)
            {
                allowedFolders.Add(filterResult.Folder);
            }
        }

        return allowedFolders;
    }

    private async Task<DriveFolderFilterResult> createDriveFolderFilterResultAsync(DriveFolderReference folder, string officialRootFolderID, CancellationToken cancellationToken)
    {
        if (folder.FolderID == officialRootFolderID)
        {
            return new DriveFolderFilterResult(folder, EPermissionDecisionState.Allowed);
        }

        bool isAllowed = await isFolderInsideOfficialRootAsync(folder.FolderID, officialRootFolderID, cancellationToken);
        EPermissionDecisionState decisionState = isAllowed ? EPermissionDecisionState.Allowed : EPermissionDecisionState.Denied;
        return new DriveFolderFilterResult(folder, decisionState);
    }

    private async Task<bool> isFolderInsideOfficialRootAsync(string folderID, string officialRootFolderID, CancellationToken cancellationToken)
    {
        string cacheKey = "OrganizationHub:FolderInsideOfficialRoot:" + officialRootFolderID + ":" + folderID;
        object? cachedValueOrNull;
        if (mMemoryCache.TryGetValue(cacheKey, out cachedValueOrNull) && cachedValueOrNull is bool cachedResult)
        {
            return cachedResult;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        bool isInsideRoot = ancestorFolderIDs.Contains(officialRootFolderID);
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
        mMemoryCache.Set(cacheKey, isInsideRoot, cacheEntryOptions);
        return isInsideRoot;
    }

    private bool isInsideOfficialRoot(IReadOnlySet<string> ancestorFolderIDs, string officialRootFolderID)
    {
        if (string.IsNullOrWhiteSpace(officialRootFolderID))
        {
            return false;
        }

        return ancestorFolderIDs.Contains(officialRootFolderID);
    }

    private async Task<bool> canReadApprovalFileAsync(UserSession userSession, string fileID, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<ApprovalRequest> approvalRequests = await dbContext.ApprovalRequests.Include(request => request.Steps).Where(request => request.FileID == fileID || request.ApprovedFileID == fileID).ToListAsync(cancellationToken);
            foreach (ApprovalRequest approvalRequest in approvalRequests)
            {
                if (canReadApprovalRequestFile(userSession, approvalRequest))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool canReadApprovalRequestFile(UserSession userSession, ApprovalRequest approvalRequest)
    {
        if (userSession.Role == EUserRole.Admin)
        {
            return true;
        }

        if (approvalRequest.RequesterUserID == userSession.UserID)
        {
            return true;
        }

        foreach (ApprovalStep step in approvalRequest.Steps)
        {
            if (step.ApproverUserID != userSession.UserID)
            {
                continue;
            }

            if (approvalRequest.Status != EApprovalRequestStatus.Pending)
            {
                return step.Status != EApprovalStepStatus.Pending;
            }

            if (step.StepNumber <= approvalRequest.CurrentStepNumber)
            {
                return true;
            }
        }

        return false;
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }

    private sealed class DriveFolderFilterResult
    {
        public DriveFolderReference Folder
        {
            get; private init;
        }
        public bool IsAllowed
        {
            get; private init;
        }

        public DriveFolderFilterResult(DriveFolderReference folder, EPermissionDecisionState decisionState)
        {
            Folder = folder;
            IsAllowed = decisionState == EPermissionDecisionState.Allowed;
        }
    }
}
