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
    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForReadableRootsAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
    {
        if (maximumCount <= 0)
        {
            return new List<UploadAuditLogItem>();
        }

        IReadOnlyList<DriveFolderReference> readableRoots = await getReadableRootsAsync(userSession, folderKind, managementTeamFilter, cancellationToken);
        if (readableRoots.Count == 0)
        {
            return new List<UploadAuditLogItem>();
        }

        int candidateCount = Math.Max(maximumCount * 10, RECENT_READABLE_ROOT_CANDIDATE_COUNT);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = createReadableRootQuery(dbContext, folderKind, readableRoots);
            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            return await filterRecentRootCandidatesAsync(candidateAuditLogs, readableRoots, maximumCount, cancellationToken);
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
            IQueryable<UploadAuditLog> query = createRootQuery(dbContext, readableRootOrNull);
            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            return await filterRootCandidatesAsync(candidateAuditLogs, readableRootOrNull, maximumCount, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<DriveFolderReference>> getReadableRootsAsync(UserSession userSession, EDriveFolderKind folderKind, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> readableRoots = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        if (managementTeamFilter == EManagementTeamFilter.ExcludeManagementTeam)
        {
            return readableRoots.Where(root => root.IsManagementTeam == false).ToList();
        }

        return readableRoots;
    }

    private IQueryable<UploadAuditLog> createReadableRootQuery(OrganizationHubDbContext dbContext, EDriveFolderKind folderKind, IReadOnlyList<DriveFolderReference> readableRoots)
    {
        List<int> readableTeamIDs = readableRoots.Where(root => root.TeamID.HasValue).Select(root => root.TeamID.GetValueOrDefault()).Distinct().ToList();
        bool canReadTeamlessRoot = readableRoots.Any(root => root.TeamID.HasValue == false);
        IQueryable<UploadAuditLog> query = dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => auditLog.FolderKind == folderKind);
        if (readableTeamIDs.Count > 0 && canReadTeamlessRoot)
        {
            return query.Where(auditLog => (auditLog.TeamID.HasValue && readableTeamIDs.Contains(auditLog.TeamID.Value)) || auditLog.TeamID.HasValue == false);
        }

        if (readableTeamIDs.Count > 0)
        {
            return query.Where(auditLog => auditLog.TeamID.HasValue && readableTeamIDs.Contains(auditLog.TeamID.Value));
        }

        return query.Where(auditLog => auditLog.TeamID.HasValue == false);
    }

    private IQueryable<UploadAuditLog> createRootQuery(OrganizationHubDbContext dbContext, DriveFolderReference readableRoot)
    {
        IQueryable<UploadAuditLog> query = dbContext.UploadAuditLogs.AsNoTracking();
        if (readableRoot.TeamID.HasValue)
        {
            int teamID = readableRoot.TeamID.Value;
            return query.Where(auditLog => auditLog.FolderKind == readableRoot.FolderKind && auditLog.TeamID == teamID);
        }

        return query.Where(auditLog => auditLog.FolderKind == readableRoot.FolderKind && auditLog.TeamID == null);
    }

    private async Task<IReadOnlyList<UploadAuditLogItem>> filterRecentRootCandidatesAsync(IReadOnlyList<UploadAuditLog> candidateAuditLogs, IReadOnlyList<DriveFolderReference> readableRoots, int maximumCount, CancellationToken cancellationToken)
    {
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

            bool isFileStillInsideRoot = await isUploadInsideRootAsync(new DriveItemID(auditLog.FileID), new DriveFolderID(matchingRootOrNull.FolderID), cancellationToken);
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

    private async Task<IReadOnlyList<UploadAuditLogItem>> filterRootCandidatesAsync(IReadOnlyList<UploadAuditLog> candidateAuditLogs, DriveFolderReference readableRoot, int maximumCount, CancellationToken cancellationToken)
    {
        List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
        foreach (UploadAuditLog auditLog in candidateAuditLogs)
        {
            bool isInsideRoot = await isUploadInsideRootAsync(new DriveItemID(auditLog.FolderID), new DriveFolderID(readableRoot.FolderID), cancellationToken);
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
