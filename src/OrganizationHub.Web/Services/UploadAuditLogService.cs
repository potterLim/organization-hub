using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService : IUploadAuditLogService
{
    private const int RECENT_ROOT_CANDIDATE_MULTIPLIER = 5;
    private const int RECENT_READABLE_ROOT_CANDIDATE_MULTIPLIER = 4;
    private const int RECENT_READABLE_ROOT_MINIMUM_CANDIDATE_COUNT = 20;
    private const int RECENT_FOLDER_UPLOAD_CANDIDATE_COUNT = 100;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;
    private readonly ILogger<UploadAuditLogService> mLogger;

    public UploadAuditLogService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IPermissionService permissionService,
        IDriveService driveService
    )
        : this(dbContextFactory, permissionService, driveService, NullLogger<UploadAuditLogService>.Instance)
    {
    }

    public UploadAuditLogService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IPermissionService permissionService,
        IDriveService driveService,
        ILogger<UploadAuditLogService> logger
    )
    {
        mDbContextFactory = dbContextFactory;
        mPermissionService = permissionService;
        mDriveService = driveService;
        mLogger = logger;
    }

    public async Task<DriveFileItem> UploadFileAsync(UserSession userSession, DriveFolderReference targetFolder, DriveFileUploadRequest request, CancellationToken cancellationToken)
    {
        if (targetFolder.FolderId != request.TargetFolderId)
        {
            throw new ArgumentException("The upload and audit folders must match.", nameof(targetFolder));
        }
        PermissionDecision decision = await mPermissionService.DecideUploadToFolderAsync(userSession, request.TargetFolderId, cancellationToken);
        if (decision.IsAllowed == false)
        {
            throw new UnauthorizedAccessException(decision.Reason);
        }
        DriveFileUploadRequest trackedRequest;
        int pendingId;
        string operationKey = Guid.NewGuid().ToString("N");
        try
        {
            DriveItemId fileId = request.ConflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile
                ? request.ReplacementFileId
                : await mDriveService.GenerateFileIdAsync(cancellationToken);
            trackedRequest = request.WithUploadIdentity(fileId, operationKey);
            DriveFileItem expectedFile = new DriveFileItem();
            expectedFile.Id = fileId.GetRequiredValue();
            expectedFile.Name = request.FileName.GetRequiredValue();
            UploadAuditLog audit = await createUploadAuditAsync(userSession, targetFolder, expectedFile, cancellationToken);
            pendingId = await persistPendingUploadAsync(audit, operationKey, cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
        {
            throw new UploadPreparationException(exception);
        }
        DriveFileItem uploadedFile = await mDriveService.UploadFileAsync(trackedRequest, cancellationToken);
        try
        {
            UploadAuditRecoveryService recovery = new UploadAuditRecoveryService(mDbContextFactory, mDriveService, mLogger);
            await recovery.ConfirmAsync(pendingId, uploadedFile, cancellationToken);
            await recovery.CompleteAsync(pendingId, cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
        {
            mLogger.LogWarning(exception, "Upload {OperationKey} is stored; its durable receipt will be recovered.", operationKey);
        }
        return uploadedFile;
    }

    public async Task RecordUploadAsync(UserSession userSession, DriveFolderReference targetFolder, DriveFileItem uploadedFile, CancellationToken cancellationToken)
    {
        UploadAuditLog audit = await createUploadAuditAsync(userSession, targetFolder, uploadedFile, cancellationToken);
        int pendingId = await persistPendingUploadAsync(audit, string.Empty, cancellationToken);
        await new UploadAuditRecoveryService(mDbContextFactory, mDriveService, mLogger).CompleteAsync(pendingId, cancellationToken);
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

            Dictionary<int, UserAccount> uploaderUsersById = await loadUploaderUsersByIdAsync(dbContext, auditLogs, cancellationToken);
            foreach (DriveFileItem file in files)
            {
                UploadAuditLog? auditLogOrNull;
                if (auditLogsByFileId.TryGetValue(file.Id, out auditLogOrNull))
                {
                    file.FolderDisplayName = auditLogOrNull.FolderDisplayName;
                    file.UploadedByDisplayName = auditLogOrNull.UploaderDisplayName;
                    file.UploadedByTeamName = getUploaderTeamName(auditLogOrNull, uploaderUsersById);
                    file.UploadedByPositionName = auditLogOrNull.UploaderPositionName;
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
            List<UploadAuditLog> auditLogs = await dbContext.UploadAuditLogs.AsNoTracking().Where(auditLog => readableFolderIds.Contains(auditLog.FolderId)).OrderByDescending(auditLog => auditLog.UploadedAt).Take(maximumCount.GetRequiredValue()).ToListAsync(cancellationToken);
            Dictionary<int, UserAccount> uploaderUsersById = await loadUploaderUsersByIdAsync(dbContext, auditLogs, cancellationToken);
            return createItems(auditLogs, uploaderUsersById);
        }
    }

    private async Task<int> persistPendingUploadAsync(UploadAuditLog audit, string operationKey, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            PendingUploadAudit pending = new PendingUploadAudit();
            pending.Payload = JsonSerializer.Serialize(audit);
            pending.OperationKey = operationKey;
            pending.CreatedAt = audit.UploadedAt;
            db.PendingUploadAudits.Add(pending);
            await db.SaveChangesAsync(cancellationToken);
            return pending.Id;
        }
    }

    private async Task<UploadAuditLog> createUploadAuditAsync(UserSession userSession, DriveFolderReference targetFolder, DriveFileItem file, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UserAccount? uploaderUserOrNull = await loadUserAccountWithAffiliationOrNullAsync(
                dbContext,
                userSession.UserAccountId,
                cancellationToken);
            UploadAuditLog auditLog = new UploadAuditLog();
            auditLog.FileId = file.Id;
            auditLog.FileName = file.Name;
            auditLog.FolderId = targetFolder.FolderId.GetRequiredValue();
            auditLog.FolderDisplayName = targetFolder.DisplayName;
            auditLog.FolderKind = targetFolder.FolderKind;
            auditLog.UploaderUserId = userSession.UserAccountId.GetRequiredValue();
            auditLog.UploaderDisplayName = userSession.DisplayName;
            auditLog.UploaderStoredCompatibilityRole = userSession.StoredCompatibilityRole;
            assignUploaderAffiliationSnapshot(auditLog, uploaderUserOrNull);
            auditLog.TeamId = targetFolder.TeamIdValueOrNull;
            auditLog.TeamName = targetFolder.TeamName;
            auditLog.UploadedAt = DateTime.UtcNow;
            return auditLog;
        }
    }

    private static async Task<UserAccount?> loadUserAccountWithAffiliationOrNullAsync(
        OrganizationHubDbContext dbContext,
        UserAccountId userAccountId,
        CancellationToken cancellationToken)
    {
        int requiredUserAccountId = userAccountId.GetRequiredValue();
        return await dbContext.UserAccounts
            .AsNoTracking()
            .Include(userAccount => userAccount.Team)
            .Include(userAccount => userAccount.Position)
            .FirstOrDefaultAsync(userAccount => userAccount.Id == requiredUserAccountId, cancellationToken);
    }

    private static async Task<Dictionary<int, UserAccount>> loadUploaderUsersByIdAsync(
        OrganizationHubDbContext dbContext,
        IReadOnlyList<UploadAuditLog> auditLogs,
        CancellationToken cancellationToken)
    {
        List<int> uploaderUserIds = auditLogs
            .Select(auditLog => auditLog.UploaderUserId)
            .Distinct()
            .ToList();
        if (uploaderUserIds.Count == 0)
        {
            return new Dictionary<int, UserAccount>();
        }

        List<UserAccount> uploaderUsers = await dbContext.UserAccounts
            .AsNoTracking()
            .Include(user => user.Team)
            .Include(user => user.Position)
            .Where(user => uploaderUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return uploaderUsers.ToDictionary(user => user.Id);
    }

    private static void assignUploaderAffiliationSnapshot(
        UploadAuditLog auditLog,
        UserAccount? uploaderUserOrNull)
    {
        if (uploaderUserOrNull == null || uploaderUserOrNull.Team == null)
        {
            auditLog.UploaderTeamId = null;
            auditLog.UploaderTeamName = string.Empty;
        }
        else
        {
            auditLog.UploaderTeamId = uploaderUserOrNull.TeamId;
            auditLog.UploaderTeamName = uploaderUserOrNull.Team.DisplayName;
        }

        if (uploaderUserOrNull == null || uploaderUserOrNull.Position == null)
        {
            auditLog.UploaderPositionId = null;
            auditLog.UploaderPositionName = string.Empty;
            return;
        }

        auditLog.UploaderPositionId = uploaderUserOrNull.PositionId;
        auditLog.UploaderPositionName = uploaderUserOrNull.Position.Name;
    }
}
