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

    public PermissionService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IDriveService driveService,
        IMemoryCache memoryCache,
        IOrganizationPermissionService organizationPermissionService
    )
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

    public async Task<IReadOnlyList<DriveFolderReference>> GetReadableDriveFoldersAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFolderReference> folders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, folderKind, cancellationToken);
        return folders;
    }

    public async Task<IReadOnlyList<DriveFolderReference>> GetUploadableDriveFoldersAsync(
        UserSession userSession,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFolderReference> folders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Uploadable, null, cancellationToken);
        return folders;
    }

    public async Task<IReadOnlyList<DriveFolderReference>> GetApprovalDestinationDriveFoldersAsync(
        UserSession userSession,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFolderReference> folders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, EDriveFolderKind.TeamApproved, cancellationToken);
        if (UserPermissionPolicy.CanUseOnlyManagementApprovalFolders(userSession.Role))
        {
            folders = folders.Where(isManagementApprovedFolder).ToList();
        }

        List<DriveFolderReference> destinationFolders = folders
            .Where(folder => folder.FolderKind == EDriveFolderKind.TeamApproved)
            .OrderBy(folder => getApprovalDestinationSortOrder(folder))
            .ThenBy(folder => folder.TeamName)
            .ThenBy(folder => folder.DisplayName)
            .ToList();
        return destinationFolders;
    }

    public async Task<bool> CanReadFileAsync(UserSession userSession, DriveItemId fileId, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideReadFileAsync(userSession, fileId, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideReadFileAsync(UserSession userSession, DriveItemId fileId, CancellationToken cancellationToken)
    {
        if (fileId.HasValue == false)
        {
            return PermissionDecision.Deny("파일을 찾을 수 없습니다.", "자료 파일");
        }

        string officialRootFolderId = string.Empty;
        bool isConfiguredSpecialFile = false;
        string requiredFileId = fileId.GetRequiredValue();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                officialRootFolderId = getTextOrEmpty(settingOrNull.OfficialRootFolderId);
                if (settingOrNull.NoticeFileId == requiredFileId || settingOrNull.WorkGuidelineFileId == requiredFileId)
                {
                    isConfiguredSpecialFile = true;
                }
            }
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(fileId, cancellationToken);
        if (isInsideOfficialRoot(ancestorFolderIds, DriveFolderId.CreateOptional(officialRootFolderId)) == false)
        {
            return PermissionDecision.Deny("공식 자료 영역 밖의 파일입니다.", "자료 파일");
        }

        if (isConfiguredSpecialFile)
        {
            return PermissionDecision.Allow("공지/업무 기준");
        }

        IReadOnlyList<DriveFolderReference> readableFolders = await GetReadableDriveFoldersAsync(userSession, cancellationToken);
        foreach (DriveFolderReference folder in readableFolders)
        {
            if (ancestorFolderIds.Contains(folder.FolderId))
            {
                return PermissionDecision.Allow(folder.DisplayName);
            }
        }

        bool hasApprovalFileAccess = await canReadApprovalFileAsync(userSession, fileId, cancellationToken);
        if (hasApprovalFileAccess)
        {
            return PermissionDecision.Allow("결재 관련 자료");
        }

        return PermissionDecision.Deny("이 파일을 볼 수 있는 자료 영역이나 결재 기록이 없습니다.", "자료 파일");
    }

    public async Task<bool> CanUploadToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideUploadToFolderAsync(userSession, folderId, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideUploadToFolderAsync(
        UserSession userSession,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        DriveFolderReference? folderOrNull = await getAllowedFolderRootForFolderAsync(userSession, folderId, EDriveFolderRootSelection.Uploadable, cancellationToken);
        if (folderOrNull == null)
        {
            return PermissionDecision.Deny("선택한 위치에 파일을 올릴 권한이 없습니다.", "파일 올리기");
        }

        return PermissionDecision.Allow(folderOrNull.DisplayName);
    }

    public async Task<bool> CanCreateFolderInFolderAsync(UserSession userSession, DriveFolderId parentFolderId, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideCreateFolderInFolderAsync(userSession, parentFolderId, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideCreateFolderInFolderAsync(
        UserSession userSession,
        DriveFolderId parentFolderId,
        CancellationToken cancellationToken
    )
    {
        PermissionDecision uploadDecision = await DecideUploadToFolderAsync(userSession, parentFolderId, cancellationToken);
        if (uploadDecision.IsAllowed)
        {
            return PermissionDecision.Allow(uploadDecision.ScopeLabel);
        }

        return PermissionDecision.Deny("이 위치에 폴더를 만들 권한이 없습니다.", uploadDecision.ScopeLabel);
    }

    public async Task<bool> CanDeleteInFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideDeleteInFolderAsync(userSession, folderId, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideDeleteInFolderAsync(
        UserSession userSession,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        DriveFolderReference? rootFolderOrNull = await getReadableFolderRootForFolderAsync(userSession, folderId, cancellationToken);
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

    private async Task<PermissionDecision> decideDeleteInsideRootAsync(
        UserSession userSession,
        DriveFolderReference rootFolder,
        CancellationToken cancellationToken
    )
    {
        EPermissionConfigurationMode configurationMode = await getPermissionConfigurationModeAsync(userSession, cancellationToken);
        if (configurationMode == EPermissionConfigurationMode.FallbackPolicy)
        {
            return DecideDeleteInsideRoot(userSession, rootFolder);
        }

        PermissionDecision permissionDecision = await mOrganizationPermissionService.DecidePermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_DELETE),
            rootFolder.TeamIdOrNull,
            cancellationToken);
        if (permissionDecision.IsAllowed)
        {
            return PermissionDecision.Allow(rootFolder.DisplayName);
        }

        return PermissionDecision.Deny("이 위치의 파일을 삭제할 권한이 없습니다.", rootFolder.DisplayName);
    }

    public async Task<bool> CanStoreApprovedFileToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideStoreApprovedFileToFolderAsync(userSession, folderId, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideStoreApprovedFileToFolderAsync(
        UserSession userSession,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        DriveFolderReference? folderOrNull = await getAllowedFolderRootForFolderAsync(userSession, folderId, EDriveFolderRootSelection.ApprovalDestination, cancellationToken);
        if (folderOrNull == null)
        {
            return PermissionDecision.Deny("이 위치에 승인본을 저장할 권한이 없습니다.", "승인본 저장");
        }

        return PermissionDecision.Allow(folderOrNull.DisplayName);
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
