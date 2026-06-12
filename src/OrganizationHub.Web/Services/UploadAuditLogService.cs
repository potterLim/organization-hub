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

public sealed class UploadAuditLogService : IUploadAuditLogService
{
    private const string GOOGLE_DRIVE_FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
    private const int RECENT_READABLE_ROOT_CANDIDATE_COUNT = 100;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;

    public UploadAuditLogService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPermissionService permissionService, IDriveService driveService)
    {
        mDbContextFactory = dbContextFactory;
        mPermissionService = permissionService;
        mDriveService = driveService;
    }

    public async Task RecordUploadAsync(UserSession userSession, DriveFolderReference targetFolder, DriveFileItem uploadedFile, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UploadAuditLog auditLog = new UploadAuditLog();
            auditLog.FileID = uploadedFile.ID;
            auditLog.FileName = uploadedFile.Name;
            auditLog.FolderID = targetFolder.FolderID;
            auditLog.FolderDisplayName = targetFolder.DisplayName;
            auditLog.FolderKind = targetFolder.FolderKind;
            auditLog.UploaderUserID = userSession.UserID;
            auditLog.UploaderDisplayName = userSession.DisplayName;
            auditLog.UploaderRole = userSession.Role;
            auditLog.TeamID = targetFolder.TeamID;
            auditLog.TeamName = targetFolder.TeamName;
            auditLog.UploadedAt = DateTime.UtcNow;
            dbContext.UploadAuditLogs.Add(auditLog);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AttachUploadMetadataAsync(IReadOnlyList<DriveFileItem> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        List<string> fileIDs = files.Select(file => file.ID).Distinct().ToList();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UploadAuditLog> auditLogs = await dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => fileIDs.Contains(auditLog.FileID)).OrderByDescending(auditLog => auditLog.UploadedAt).ToListAsync(cancellationToken);
            Dictionary<string, UploadAuditLog> auditLogsByFileID = new Dictionary<string, UploadAuditLog>();
            foreach (UploadAuditLog auditLog in auditLogs)
            {
                if (auditLogsByFileID.ContainsKey(auditLog.FileID) == false)
                {
                    auditLogsByFileID.Add(auditLog.FileID, auditLog);
                }
            }

            foreach (DriveFileItem file in files)
            {
                UploadAuditLog? auditLogOrNull;
                if (auditLogsByFileID.TryGetValue(file.ID, out auditLogOrNull))
                {
                    file.FolderDisplayName = auditLogOrNull.FolderDisplayName;
                    file.UploadedByDisplayName = auditLogOrNull.UploaderDisplayName;
                    file.UploadedAt = auditLogOrNull.UploadedAt;
                }
            }
        }
    }

    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsAsync(UserSession userSession, int maximumCount, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> readableFolders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, cancellationToken);
        HashSet<string> readableFolderIDs = new HashSet<string>(readableFolders.Select(folder => folder.FolderID));
        if (readableFolderIDs.Count == 0)
        {
            return new List<UploadAuditLogItem>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UploadAuditLog> auditLogs = await dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => readableFolderIDs.Contains(auditLog.FolderID)).OrderByDescending(auditLog => auditLog.UploadedAt).Take(maximumCount).ToListAsync(cancellationToken);
            List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            foreach (UploadAuditLog auditLog in auditLogs)
            {
                items.Add(createItem(auditLog));
            }

            return items;
        }
    }

    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForReadableRootsAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
    {
        if (maximumCount <= 0)
        {
            return new List<UploadAuditLogItem>();
        }

        IReadOnlyList<DriveFolderReference> readableRoots = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        if (managementTeamFilter == EManagementTeamFilter.ExcludeManagementTeam)
        {
            readableRoots = readableRoots.Where(root => root.IsManagementTeam == false).ToList();
        }

        if (readableRoots.Count == 0)
        {
            return new List<UploadAuditLogItem>();
        }

        List<int> readableTeamIDs = readableRoots.Where(root => root.TeamID.HasValue).Select(root => root.TeamID.GetValueOrDefault()).Distinct().ToList();
        bool canReadTeamlessRoot = readableRoots.Any(root => root.TeamID.HasValue == false);
        int candidateCount = Math.Max(maximumCount * 10, RECENT_READABLE_ROOT_CANDIDATE_COUNT);

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => auditLog.FolderKind == folderKind);
            if (readableTeamIDs.Count > 0 && canReadTeamlessRoot)
            {
                query = query.Where(auditLog => (auditLog.TeamID.HasValue && readableTeamIDs.Contains(auditLog.TeamID.Value)) || auditLog.TeamID.HasValue == false);
            }
            else if (readableTeamIDs.Count > 0)
            {
                query = query.Where(auditLog => auditLog.TeamID.HasValue && readableTeamIDs.Contains(auditLog.TeamID.Value));
            }
            else
            {
                query = query.Where(auditLog => auditLog.TeamID.HasValue == false);
            }

            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            foreach (UploadAuditLog auditLog in candidateAuditLogs)
            {
                DriveFolderReference? matchingRootOrNull = await getMatchingReadableRootOrNullAsync(auditLog, readableRoots, cancellationToken);
                if (matchingRootOrNull == null)
                {
                    continue;
                }

                DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(auditLog.FileID, cancellationToken);
                if (fileMetadataOrNull == null || fileMetadataOrNull.MimeType == GOOGLE_DRIVE_FOLDER_MIME_TYPE)
                {
                    continue;
                }

                bool isFileStillInsideRoot = await isUploadInsideRootAsync(auditLog.FileID, matchingRootOrNull.FolderID, cancellationToken);
                if (isFileStillInsideRoot == false)
                {
                    continue;
                }

                items.Add(createItem(auditLog, fileMetadataOrNull));
                if (items.Count >= maximumCount)
                {
                    break;
                }
            }

            return items;
        }
    }

    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForRootAsync(UserSession userSession, DriveFolderReference rootFolder, int maximumCount, CancellationToken cancellationToken)
    {
        if (maximumCount <= 0 || string.IsNullOrWhiteSpace(rootFolder.FolderID))
        {
            return new List<UploadAuditLogItem>();
        }

        DriveFolderReference? readableRootOrNull = await getReadableRootOrNullAsync(userSession, rootFolder, cancellationToken);
        if (readableRootOrNull == null)
        {
            return new List<UploadAuditLogItem>();
        }

        int candidateCount = Math.Max(maximumCount * 5, maximumCount);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = dbContext.UploadAuditLogs.AsNoTracking();
            if (readableRootOrNull.TeamID.HasValue)
            {
                int teamID = readableRootOrNull.TeamID.Value;
                query = query.Where(auditLog => auditLog.FolderKind == readableRootOrNull.FolderKind && auditLog.TeamID == teamID);
            }
            else
            {
                query = query.Where(auditLog => auditLog.FolderKind == readableRootOrNull.FolderKind && auditLog.TeamID == null);
            }

            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            foreach (UploadAuditLog auditLog in candidateAuditLogs)
            {
                bool isInsideRoot = await isUploadInsideRootAsync(auditLog.FolderID, readableRootOrNull.FolderID, cancellationToken);
                if (isInsideRoot == false)
                {
                    continue;
                }

                items.Add(createItem(auditLog));
                if (items.Count >= maximumCount)
                {
                    break;
                }
            }

            return items;
        }
    }

    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForFolderAsync(string folderID, TimeSpan maximumAge, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderID))
        {
            return new List<UploadAuditLogItem>();
        }

        DateTime cutoff = DateTime.UtcNow.Subtract(maximumAge);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UploadAuditLog> auditLogs = await dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => auditLog.FolderID == folderID.Trim() && auditLog.UploadedAt >= cutoff).OrderByDescending(auditLog => auditLog.UploadedAt).Take(100).ToListAsync(cancellationToken);
            List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            foreach (UploadAuditLog auditLog in auditLogs)
            {
                items.Add(createItem(auditLog));
            }

            return items;
        }
    }

    private async Task<DriveFolderReference?> getReadableRootOrNullAsync(UserSession userSession, DriveFolderReference rootFolder, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> readableFolders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, rootFolder.FolderKind, cancellationToken);
        foreach (DriveFolderReference readableFolder in readableFolders)
        {
            if (isSameRoot(readableFolder, rootFolder))
            {
                return readableFolder;
            }
        }

        return null;
    }

    private static bool isSameRoot(DriveFolderReference left, DriveFolderReference right)
    {
        if (left.FolderID != right.FolderID)
        {
            return false;
        }

        if (left.FolderKind != right.FolderKind)
        {
            return false;
        }

        return left.TeamID == right.TeamID;
    }

    private async Task<bool> isUploadInsideRootAsync(string folderID, string rootFolderID, CancellationToken cancellationToken)
    {
        if (folderID == rootFolderID)
        {
            return true;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        return ancestorFolderIDs.Contains(rootFolderID);
    }

    private async Task<DriveFolderReference?> getMatchingReadableRootOrNullAsync(UploadAuditLog auditLog, IReadOnlyList<DriveFolderReference> readableRoots, CancellationToken cancellationToken)
    {
        foreach (DriveFolderReference readableRoot in readableRoots)
        {
            if (isAuditLogCandidateForRoot(auditLog, readableRoot) == false)
            {
                continue;
            }

            bool isFolderInsideRoot = await isUploadInsideRootAsync(auditLog.FolderID, readableRoot.FolderID, cancellationToken);
            if (isFolderInsideRoot)
            {
                return readableRoot;
            }
        }

        return null;
    }

    private static bool isAuditLogCandidateForRoot(UploadAuditLog auditLog, DriveFolderReference readableRoot)
    {
        if (auditLog.FolderKind != readableRoot.FolderKind)
        {
            return false;
        }

        if (readableRoot.TeamID.HasValue)
        {
            return auditLog.TeamID.HasValue && auditLog.TeamID.Value == readableRoot.TeamID.Value;
        }

        return auditLog.TeamID.HasValue == false;
    }

    private UploadAuditLogItem createItem(UploadAuditLog auditLog)
    {
        return createItem(auditLog, null);
    }

    private UploadAuditLogItem createItem(UploadAuditLog auditLog, DriveFileMetadata? fileMetadataOrNull)
    {
        UploadAuditLogItem item = new UploadAuditLogItem();
        item.FileID = auditLog.FileID;
        item.FileName = fileMetadataOrNull == null || string.IsNullOrWhiteSpace(fileMetadataOrNull.Name) ? auditLog.FileName : fileMetadataOrNull.Name;
        item.FolderDisplayName = auditLog.FolderDisplayName;
        item.FolderKind = auditLog.FolderKind;
        item.UploaderDisplayName = auditLog.UploaderDisplayName;
        item.TeamName = auditLog.TeamName;
        item.UploadedAt = auditLog.UploadedAt;
        return item;
    }
}
