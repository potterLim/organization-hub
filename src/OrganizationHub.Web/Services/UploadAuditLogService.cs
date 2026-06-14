using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService : IUploadAuditLogService
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
            return createItems(auditLogs);
        }
    }
}
