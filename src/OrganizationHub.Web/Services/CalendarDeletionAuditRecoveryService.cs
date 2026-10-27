using System;
using System.Collections.Generic;
using System.Linq;
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

internal sealed class CalendarDeletionAuditRecoveryService
{
    private const int RECOVERY_BATCH_SIZE = 20;
    private const int STALE_DELETION_MINUTES = 5;
    private readonly IDbContextFactory<OrganizationHubDbContext> mFactory;
    private readonly IGoogleApiCredentialFactory mCredentials;
    private readonly ILogger mLogger;

    public CalendarDeletionAuditRecoveryService(IDbContextFactory<OrganizationHubDbContext> factory, IGoogleApiCredentialFactory credentials, ILogger logger)
    {
        mFactory = factory;
        mCredentials = credentials;
        mLogger = logger;
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        List<CalendarEventDeletionAuditLog> deletions;
        DateTime staleBefore = DateTime.UtcNow.AddMinutes(-STALE_DELETION_MINUTES);
        await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
        {
            deletions = await db.CalendarEventDeletionAuditLogs.AsNoTracking().Where(item => item.Status == ECalendarEventDeletionAuditStatus.Pending && item.DeletedAt < staleBefore).OrderBy(item => item.LastRecoveryAttemptAt).ThenBy(item => item.Id).Take(RECOVERY_BATCH_SIZE).ToListAsync(cancellationToken);
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
                    await markAttemptAsync(deletion.Id, cancellationToken);
                    bool isDeleted = await ResourceDeletionConfirmation.IsDeletedAsync(
                        () => isCancelledAsync(calendar, deletion, cancellationToken),
                        () => ensureCalendarReadableAsync(calendar, deletion.CalendarId, cancellationToken));
                    ECalendarEventDeletionAuditStatus status = isDeleted ? ECalendarEventDeletionAuditStatus.Succeeded : ECalendarEventDeletionAuditStatus.Failed;
                    await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
                    {
                        await db.CalendarEventDeletionAuditLogs.Where(item => item.Id == deletion.Id && item.Status == ECalendarEventDeletionAuditStatus.Pending).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status), cancellationToken);
                    }
                }
                catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
                {
                    mLogger.LogError(exception, "Deletion audit {AuditId} requires recovery; retrying automatically.", deletion.Id);
                }
            }
        }
    }

    private async Task markAttemptAsync(int auditId, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await mFactory.CreateDbContextAsync(cancellationToken))
        {
            await db.CalendarEventDeletionAuditLogs.Where(item => item.Id == auditId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LastRecoveryAttemptAt, DateTime.UtcNow), cancellationToken);
        }
    }

    private static async Task<bool> isCancelledAsync(CalendarService calendar, CalendarEventDeletionAuditLog deletion, CancellationToken cancellationToken)
    {
        return (await calendar.Events.Get(deletion.CalendarId, deletion.EventId).ExecuteAsync(cancellationToken)).Status == "cancelled";
    }

    private static async Task ensureCalendarReadableAsync(CalendarService calendar, string calendarId, CancellationToken cancellationToken)
    {
        EventsResource.ListRequest confirmation = calendar.Events.List(calendarId);
        confirmation.MaxResults = 1;
        confirmation.Fields = "kind";
        await confirmation.ExecuteAsync(cancellationToken);
    }
}
