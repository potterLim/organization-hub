using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class UploadAuditRecoveryService
{
    private const int RECOVERY_BATCH_SIZE = 20;
    private readonly IDbContextFactory<OrganizationHubDbContext> mFactory;
    private readonly IDriveService? mDriveServiceOrNull;
    private readonly ILogger mLogger;

    public UploadAuditRecoveryService(IDbContextFactory<OrganizationHubDbContext> factory, IDriveService? driveServiceOrNull, ILogger logger)
    {
        mFactory = factory;
        mDriveServiceOrNull = driveServiceOrNull;
        mLogger = logger;
    }

    public async Task CompleteAsync(int pendingId, CancellationToken cancellationToken)
    {
        PendingUploadAudit? pendingOrNull;
        await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
        {
            pendingOrNull = await db.PendingUploadAudits.AsNoTracking().SingleOrDefaultAsync(item => item.Id == pendingId, cancellationToken);
        }
        if (pendingOrNull == null)
        {
            return;
        }
        UploadAuditLog? auditOrNull = JsonSerializer.Deserialize<UploadAuditLog>(pendingOrNull.Payload);
        if (auditOrNull == null)
        {
            throw new InvalidDataException("The queued upload receipt is empty.");
        }
        if (pendingOrNull.OperationKey.Length > 0 && pendingOrNull.IsUploadConfirmed == false)
        {
            if (mDriveServiceOrNull == null)
            {
                throw new InvalidOperationException("Upload reconciliation requires a Drive service.");
            }
            DriveFileItem? fileOrNull = await mDriveServiceOrNull.GetUploadReceiptOrNullAsync(DriveItemId.CreateRequired(auditOrNull.FileId), pendingOrNull.OperationKey, cancellationToken);
            if (fileOrNull == null)
            {
                return;
            }
            auditOrNull.FileName = fileOrNull.Name;
            await ConfirmAsync(pendingId, fileOrNull, cancellationToken);
        }
        await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
        await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            PendingUploadAudit? currentOrNull = await db.PendingUploadAudits.FindAsync(new object[] { pendingId }, cancellationToken);
            if (currentOrNull == null)
            {
                return;
            }
            db.UploadAuditLogs.Add(auditOrNull);
            db.PendingUploadAudits.Remove(currentOrNull);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        List<int> pendingIds;
        await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
        {
            pendingIds = await db.PendingUploadAudits.OrderBy(item => item.LastAttemptAt).ThenBy(item => item.Id).Select(item => item.Id).Take(RECOVERY_BATCH_SIZE).ToListAsync(cancellationToken);
        }
        foreach (int pendingId in pendingIds)
        {
            try
            {
                await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
                {
                    await db.PendingUploadAudits.Where(item => item.Id == pendingId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LastAttemptAt, DateTime.UtcNow), cancellationToken);
                }
                await CompleteAsync(pendingId, cancellationToken);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
            {
                mLogger.LogError(exception, "Upload audit {PendingId} requires recovery; retrying automatically.", pendingId);
            }
        }
    }

    public async Task ConfirmAsync(int pendingId, DriveFileItem file, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
        {
            PendingUploadAudit? pendingOrNull = await db.PendingUploadAudits.FindAsync(new object[] { pendingId }, cancellationToken);
            if (pendingOrNull == null)
            {
                return;
            }
            UploadAuditLog? auditOrNull = JsonSerializer.Deserialize<UploadAuditLog>(pendingOrNull.Payload);
            if (auditOrNull == null || auditOrNull.FileId != file.Id)
            {
                throw new InvalidDataException("The confirmed file does not match its upload journal.");
            }
            auditOrNull.FileName = file.Name;
            pendingOrNull.Payload = JsonSerializer.Serialize(auditOrNull);
            pendingOrNull.IsUploadConfirmed = true;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
