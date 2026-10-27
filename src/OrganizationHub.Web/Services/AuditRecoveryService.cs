using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

internal sealed class AuditRecoveryService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mFactory;
    private readonly IGoogleApiCredentialFactory mCredentials;
    private readonly ILogger<AuditRecoveryService> mLogger;

    public AuditRecoveryService(IDbContextFactory<OrganizationHubDbContext> factory, IGoogleApiCredentialFactory credentials, ILogger<AuditRecoveryService> logger)
    {
        mFactory = factory;
        mCredentials = credentials;
        mLogger = logger;
    }

    public static async Task CompleteUploadAuditAsync(IDbContextFactory<OrganizationHubDbContext> factory, int pendingId, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await factory.CreateDbContextAsync(cancellationToken))
        await using (Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            PendingUploadAudit? pendingOrNull = await db.PendingUploadAudits.FindAsync(new object[] { pendingId }, cancellationToken);
            if (pendingOrNull == null)
            {
                return;
            }
            UploadAuditLog audit = JsonSerializer.Deserialize<UploadAuditLog>(pendingOrNull.Payload)!;
            db.UploadAuditLogs.Add(audit);
            db.PendingUploadAudits.Remove(pendingOrNull);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        List<int> uploads;
        List<CalendarEventDeletionAuditLog> deletions;
        DateTime staleBefore = DateTime.UtcNow.AddMinutes(-5);
        await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
        {
            uploads = await db.PendingUploadAudits.OrderBy(item => item.Id).Select(item => item.Id).Take(20).ToListAsync(cancellationToken);
            deletions = await db.CalendarEventDeletionAuditLogs.AsNoTracking().Where(item => item.Status == ECalendarEventDeletionAuditStatus.Pending && item.DeletedAt < staleBefore).OrderBy(item => item.Id).Take(20).ToListAsync(cancellationToken);
        }
        foreach (int id in uploads)
        {
            try
            {
                await CompleteUploadAuditAsync(mFactory, id, cancellationToken);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
            {
                mLogger.LogError(exception, "Upload audit {PendingId} requires recovery; retrying automatically.", id);
            }
        }
        if (deletions.Count == 0)
        {
            return;
        }
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = await mCredentials.CreateHttpClientInitializerAsync(new string[] { CalendarService.Scope.Calendar }, cancellationToken);
        using (CalendarService calendar = new CalendarService(initializer))
        {
            foreach (CalendarEventDeletionAuditLog deletion in deletions)
            {
                try
                {
                    bool isDeleted = await ResourceDeletionConfirmation.IsDeletedAsync(async () => (await calendar.Events.Get(deletion.CalendarId, deletion.EventId).ExecuteAsync(cancellationToken)).Status == "cancelled");
                    ECalendarEventDeletionAuditStatus status = isDeleted ? ECalendarEventDeletionAuditStatus.Succeeded : ECalendarEventDeletionAuditStatus.Failed;
                    await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
                    {
                        await db.CalendarEventDeletionAuditLogs.Where(item => item.Id == deletion.Id && item.Status == ECalendarEventDeletionAuditStatus.Pending)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken);
                    }
                }
                catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
                {
                    mLogger.LogError(exception, "Deletion audit {AuditId} requires recovery; retrying automatically.", deletion.Id);
                }
            }
        }
    }
}
