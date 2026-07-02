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

        int maximumItemCount = maximumCount.GetRequiredValue();
        int candidateCount = Math.Max(maximumItemCount * RECENT_READABLE_ROOT_CANDIDATE_MULTIPLIER, RECENT_READABLE_ROOT_MINIMUM_CANDIDATE_COUNT);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = createReadableRootQuery(dbContext, folderKind, readableRoots);
            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            Dictionary<int, UserAccount> uploaderUsersById = await loadUploaderUsersByIdAsync(dbContext, candidateAuditLogs, cancellationToken);
            RecentReadableRootUploadFilterContext filterContext = new RecentReadableRootUploadFilterContext
            {
                CandidateAuditLogs = candidateAuditLogs,
                ReadableRoots = readableRoots,
                MaximumCount = maximumCount,
                UploaderUsersById = uploaderUsersById,
                CancellationToken = cancellationToken
            };
            return await filterRecentRootCandidatesAsync(filterContext);
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

        int maximumItemCount = maximumCount.GetRequiredValue();
        int candidateCount = Math.Max(maximumItemCount * RECENT_ROOT_CANDIDATE_MULTIPLIER, maximumItemCount);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = createRootQuery(dbContext, readableRootOrNull);
            List<UploadAuditLog> candidateAuditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(candidateCount).ToListAsync(cancellationToken);
            Dictionary<int, UserAccount> uploaderUsersById = await loadUploaderUsersByIdAsync(dbContext, candidateAuditLogs, cancellationToken);
            RecentRootUploadFilterContext filterContext = new RecentRootUploadFilterContext
            {
                CandidateAuditLogs = candidateAuditLogs,
                ReadableRoot = readableRootOrNull,
                MaximumCount = maximumCount,
                UploaderUsersById = uploaderUsersById,
                CancellationToken = cancellationToken
            };
            return await filterRootCandidatesAsync(filterContext);
        }
    }

    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadSnapshotsForReadableRootsAsync(
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

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UploadAuditLog> query = createReadableRootQuery(dbContext, folderKind, readableRoots);
            List<UploadAuditLog> auditLogs = await query.OrderByDescending(auditLog => auditLog.UploadedAt).Take(maximumCount.GetRequiredValue()).ToListAsync(cancellationToken);
            Dictionary<int, UserAccount> uploaderUsersById = await loadUploaderUsersByIdAsync(dbContext, auditLogs, cancellationToken);
            return createItems(auditLogs, uploaderUsersById);
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

    private async Task<IReadOnlyList<UploadAuditLogItem>> filterRecentRootCandidatesAsync(RecentReadableRootUploadFilterContext filterContext)
    {
        List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
        HashSet<string> addedFileIds = new HashSet<string>();
        foreach (UploadAuditLog auditLog in filterContext.CandidateAuditLogs)
        {
            if (shouldSkipDuplicateRecentFile(auditLog, addedFileIds))
            {
                continue;
            }

            UploadAuditLogItem? itemOrNull = await tryCreateRecentReadableRootItemOrNullAsync(filterContext, auditLog);
            if (itemOrNull == null)
            {
                continue;
            }

            items.Add(itemOrNull);
            if (items.Count >= filterContext.MaximumCount.GetRequiredValue())
            {
                break;
            }
        }

        return items;
    }

    private async Task<IReadOnlyList<UploadAuditLogItem>> filterRootCandidatesAsync(RecentRootUploadFilterContext filterContext)
    {
        List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
        HashSet<string> addedFileIds = new HashSet<string>();
        foreach (UploadAuditLog auditLog in filterContext.CandidateAuditLogs)
        {
            if (shouldSkipDuplicateRecentFile(auditLog, addedFileIds))
            {
                continue;
            }

            UploadAuditLogItem? itemOrNull = await tryCreateRecentRootItemOrNullAsync(filterContext, auditLog);
            if (itemOrNull == null)
            {
                continue;
            }

            items.Add(itemOrNull);
            if (items.Count >= filterContext.MaximumCount.GetRequiredValue())
            {
                break;
            }
        }

        return items;
    }

    private sealed class RecentReadableRootUploadFilterContext
    {
        public required IReadOnlyList<UploadAuditLog> CandidateAuditLogs
        {
            get; init;
        }

        public required IReadOnlyList<DriveFolderReference> ReadableRoots
        {
            get; init;
        }

        public required RecentItemLimit MaximumCount
        {
            get; init;
        }

        public required IReadOnlyDictionary<int, UserAccount> UploaderUsersById
        {
            get; init;
        }

        public required CancellationToken CancellationToken
        {
            get; init;
        }
    }

    private sealed class RecentRootUploadFilterContext
    {
        public required IReadOnlyList<UploadAuditLog> CandidateAuditLogs
        {
            get; init;
        }

        public required DriveFolderReference ReadableRoot
        {
            get; init;
        }

        public required RecentItemLimit MaximumCount
        {
            get; init;
        }

        public required IReadOnlyDictionary<int, UserAccount> UploaderUsersById
        {
            get; init;
        }

        public required CancellationToken CancellationToken
        {
            get; init;
        }
    }
}
