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

public sealed partial class UploadAuditLogService : IUploadAuditLogService
{
    private const string GOOGLE_DRIVE_FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
    private const int RECENT_READABLE_ROOT_CANDIDATE_COUNT = 100;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;

    public UploadAuditLogService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IPermissionService permissionService,
        IDriveService driveService
    )
    {
        mDbContextFactory = dbContextFactory;
        mPermissionService = permissionService;
        mDriveService = driveService;
    }

    public async Task RecordUploadAsync(
        UserSession userSession,
        DriveFolderReference targetFolder,
        DriveFileItem uploadedFile,
        CancellationToken cancellationToken
    )
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UploadAuditLog auditLog = new UploadAuditLog();
            auditLog.FileId = uploadedFile.Id;
            auditLog.FileName = uploadedFile.Name;
            auditLog.FolderId = targetFolder.FolderId.GetRequiredValue();
            auditLog.FolderDisplayName = targetFolder.DisplayName;
            auditLog.FolderKind = targetFolder.FolderKind;
            auditLog.UploaderUserId = userSession.UserId;
            auditLog.UploaderDisplayName = userSession.DisplayName;
            auditLog.UploaderRole = userSession.Role;
            auditLog.TeamId = targetFolder.TeamIdValueOrNull;
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

        List<string> fileIds = files.Select(file => file.Id).Distinct().ToList();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UploadAuditLog> auditLogs = await dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => fileIds.Contains(auditLog.FileId)).OrderByDescending(auditLog => auditLog.UploadedAt).ToListAsync(cancellationToken);
            Dictionary<string, UploadAuditLog> auditLogsByFileId = new Dictionary<string, UploadAuditLog>();
            foreach (UploadAuditLog auditLog in auditLogs)
            {
                if (auditLogsByFileId.ContainsKey(auditLog.FileId) == false)
                {
                    auditLogsByFileId.Add(auditLog.FileId, auditLog);
                }
            }

            foreach (DriveFileItem file in files)
            {
                UploadAuditLog? auditLogOrNull;
                if (auditLogsByFileId.TryGetValue(file.Id, out auditLogOrNull))
                {
                    file.FolderDisplayName = auditLogOrNull.FolderDisplayName;
                    file.UploadedByDisplayName = auditLogOrNull.UploaderDisplayName;
                    file.UploadedAt = auditLogOrNull.UploadedAt;
                }
            }
        }
    }

    public async Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsAsync(
        UserSession userSession,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFolderReference> readableFolders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, cancellationToken);
        HashSet<string> readableFolderIds = new HashSet<string>(readableFolders.Select(folder => folder.FolderId.GetRequiredValue()));
        if (readableFolderIds.Count == 0)
        {
            return new List<UploadAuditLogItem>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<UploadAuditLog> auditLogs = await dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => readableFolderIds.Contains(auditLog.FolderId)).OrderByDescending(auditLog => auditLog.UploadedAt).Take(maximumCount.Value).ToListAsync(cancellationToken);
            return createItems(auditLogs);
        }
    }
}
