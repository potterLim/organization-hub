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

public sealed partial class UploadAuditLogService
{
    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForReadableRootsAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFolderReference> readableRoots = await getReadableRootsAsync(userSession, folderKind, managementTeamFilter, cancellationToken);
        if (readableRoots.Count == 0)
        {
            return new List<UploadAuditLogItem>();
        }

        int candidateCount = Math.Max(maximumCount.Value * 10, RECENT_READABLE_ROOT_CANDIDATE_COUNT);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = createReadableRootQuery(dbContext, folderKind, readableRoots);
            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            return await filterRecentRootCandidatesAsync(candidateAuditLogs, readableRoots, maximumCount, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForRootAsync(
        UserSession userSession,
        DriveFolderReference rootFolder,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken
    )
    {
        if (rootFolder.FolderId.HasValue == false)
        {
            return new List<UploadAuditLogItem>();
        }

        DriveFolderReference? readableRootOrNull = await getReadableRootOrNullAsync(userSession, rootFolder, cancellationToken);
        if (readableRootOrNull == null)
        {
            return new List<UploadAuditLogItem>();
        }

        int candidateCount = Math.Max(maximumCount.Value * 5, maximumCount.Value);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = createRootQuery(dbContext, readableRootOrNull);
            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            return await filterRootCandidatesAsync(candidateAuditLogs, readableRootOrNull, maximumCount, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<DriveFolderReference>> getReadableRootsAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        EManagementTeamFilter managementTeamFilter,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFolderReference> readableRoots = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        if (managementTeamFilter == EManagementTeamFilter.ExcludeManagementTeam)
        {
            return readableRoots.Where(root => root.IsManagementTeam == false).ToList();
        }

        return readableRoots;
    }

    private IQueryable<UploadAuditLog> createReadableRootQuery(
        OrganizationHubDbContext dbContext,
        EDriveFolderKind folderKind,
        IReadOnlyList<DriveFolderReference> readableRoots
    )
    {
        List<int> readableTeamIds = new List<int>();
        foreach (DriveFolderReference root in readableRoots)
        {
            int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(root.TeamIdOrNull);
            if (teamIdValueOrNull.HasValue)
            {
                readableTeamIds.Add(teamIdValueOrNull.GetValueOrDefault());
            }
        }

        readableTeamIds = readableTeamIds.Distinct().ToList();
        bool canReadTeamlessRoot = readableRoots.Any(root => root.TeamIdOrNull.HasValue == false);
        IQueryable<UploadAuditLog> query = dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => auditLog.FolderKind == folderKind);
        if (readableTeamIds.Count > 0 && canReadTeamlessRoot)
        {
            return query.Where(auditLog => (auditLog.TeamId.HasValue && readableTeamIds.Contains(auditLog.TeamId.Value)) || auditLog.TeamId.HasValue == false);
        }

        if (readableTeamIds.Count > 0)
        {
            return query.Where(auditLog => auditLog.TeamId.HasValue && readableTeamIds.Contains(auditLog.TeamId.Value));
        }

        return query.Where(auditLog => auditLog.TeamId.HasValue == false);
    }

    private IQueryable<UploadAuditLog> createRootQuery(OrganizationHubDbContext dbContext, DriveFolderReference readableRoot)
    {
        IQueryable<UploadAuditLog> query = dbContext.UploadAuditLogs.AsNoTracking();
        int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(readableRoot.TeamIdOrNull);
        if (teamIdValueOrNull.HasValue)
        {
            int teamId = teamIdValueOrNull.GetValueOrDefault();
            return query.Where(auditLog => auditLog.FolderKind == readableRoot.FolderKind && auditLog.TeamId == teamId);
        }

        return query.Where(auditLog => auditLog.FolderKind == readableRoot.FolderKind && auditLog.TeamId == null);
    }

    private async Task<IReadOnlyList<UploadAuditLogItem>> filterRecentRootCandidatesAsync(
        IReadOnlyList<UploadAuditLog> candidateAuditLogs,
        IReadOnlyList<DriveFolderReference> readableRoots,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken
    )
    {
        List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
        foreach (UploadAuditLog auditLog in candidateAuditLogs)
        {
            DriveFolderReference? matchingRootOrNull = await getMatchingReadableRootOrNullAsync(auditLog, readableRoots, cancellationToken);
            if (matchingRootOrNull == null)
            {
                continue;
            }

            DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateOptional(auditLog.FileId), cancellationToken);
            if (fileMetadataOrNull == null || fileMetadataOrNull.MimeType == GOOGLE_DRIVE_FOLDER_MIME_TYPE)
            {
                continue;
            }

            bool isFileStillInsideRoot = await isUploadInsideRootAsync(DriveItemId.CreateOptional(auditLog.FileId), matchingRootOrNull.FolderId, cancellationToken);
            if (isFileStillInsideRoot == false)
            {
                continue;
            }

            items.Add(createItem(auditLog, fileMetadataOrNull));
            if (items.Count >= maximumCount.Value)
            {
                break;
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<UploadAuditLogItem>> filterRootCandidatesAsync(
        IReadOnlyList<UploadAuditLog> candidateAuditLogs,
        DriveFolderReference readableRoot,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken
    )
    {
        List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
        foreach (UploadAuditLog auditLog in candidateAuditLogs)
        {
            bool isInsideRoot = await isUploadInsideRootAsync(DriveItemId.CreateOptional(auditLog.FolderId), readableRoot.FolderId, cancellationToken);
            if (isInsideRoot == false)
            {
                continue;
            }

            items.Add(createItem(auditLog));
            if (items.Count >= maximumCount.Value)
            {
                break;
            }
        }

        return items;
    }
}
