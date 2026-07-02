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

public sealed class CalendarEventDeletionAuditLogService : ICalendarEventDeletionAuditLogService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public CalendarEventDeletionAuditLogService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IOrganizationPermissionService organizationPermissionService)
    {
        mDbContextFactory = dbContextFactory;
        mOrganizationPermissionService = organizationPermissionService;
    }

    public async Task<CalendarEventDeletionAuditLogId> RecordPendingDeletionAsync(
        UserSession userSession,
        CalendarEventItem calendarEvent,
        CancellationToken cancellationToken
    )
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UserAccount? deletedByUserOrNull = await loadUserAccountWithPositionOrNullAsync(
                dbContext,
                userSession.UserAccountId,
                cancellationToken);
            CalendarEventDeletionAuditLog auditLog = new CalendarEventDeletionAuditLog();
            auditLog.CalendarId = calendarEvent.CalendarId;
            auditLog.CalendarName = calendarEvent.CalendarName;
            auditLog.EventId = calendarEvent.Id;
            auditLog.EventSummary = calendarEvent.Summary;
            auditLog.EventLocation = calendarEvent.Location;
            auditLog.EventStart = calendarEvent.Start;
            auditLog.EventEnd = calendarEvent.End;
            auditLog.IsAllDay = calendarEvent.IsAllDay;
            auditLog.DeletedByUserId = userSession.UserAccountId.GetRequiredValue();
            auditLog.DeletedByDisplayName = userSession.DisplayName;
            auditLog.DeletedByStoredCompatibilityRole = userSession.StoredCompatibilityRole;
            assignDeletedByPositionSnapshot(auditLog, deletedByUserOrNull);
            auditLog.DeletedByTeamId = OrganizationTeamId.GetValueOrNull(userSession.OrganizationTeamIdOrNull);
            auditLog.DeletedByTeamName = userSession.TeamDisplayName;
            auditLog.DeletedAt = DateTime.UtcNow;
            auditLog.Status = ECalendarEventDeletionAuditStatus.Pending;
            dbContext.CalendarEventDeletionAuditLogs.Add(auditLog);
            await dbContext.SaveChangesAsync(cancellationToken);
            return CalendarEventDeletionAuditLogId.CreateExisting(auditLog.Id);
        }
    }

    public async Task MarkDeletionSucceededAsync(CalendarEventDeletionAuditLogId auditLogId, CancellationToken cancellationToken)
    {
        await updateStatusAsync(auditLogId, ECalendarEventDeletionAuditStatus.Succeeded, CalendarEventDeletionFailureMessage.Empty, cancellationToken);
    }

    public async Task MarkDeletionFailedAsync(CalendarEventDeletionAuditLogId auditLogId, CalendarEventDeletionFailureMessage failureMessage, CancellationToken cancellationToken)
    {
        await updateStatusAsync(auditLogId, ECalendarEventDeletionAuditStatus.Failed, failureMessage, cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEventDeletionAuditLogItem>> GetRecentDeletedEventsAsync(
        UserSession userSession,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken
    )
    {
        bool canReadAuditLogs = await mOrganizationPermissionService.HasPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.CALENDAR_AUDIT_READ),
            null,
            cancellationToken);
        if (canReadAuditLogs == false)
        {
            return new List<CalendarEventDeletionAuditLogItem>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<CalendarEventDeletionAuditLog> auditLogs = await dbContext.CalendarEventDeletionAuditLogs.OrderByDescending(auditLog => auditLog.DeletedAt).ThenByDescending(auditLog => auditLog.Id).Take(maximumCount.GetRequiredValue()).ToListAsync(cancellationToken);
            List<CalendarEventDeletionAuditLogItem> items = new List<CalendarEventDeletionAuditLogItem>();
            foreach (CalendarEventDeletionAuditLog auditLog in auditLogs)
            {
                items.Add(createItem(auditLog));
            }

            return items;
        }
    }

    private async Task updateStatusAsync(
        CalendarEventDeletionAuditLogId auditLogId,
        ECalendarEventDeletionAuditStatus status,
        CalendarEventDeletionFailureMessage failureMessage,
        CancellationToken cancellationToken
    )
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredAuditLogId = auditLogId.GetRequiredValue();
            CalendarEventDeletionAuditLog? auditLogOrNull = await dbContext.CalendarEventDeletionAuditLogs.FirstOrDefaultAsync(auditLog => auditLog.Id == requiredAuditLogId, cancellationToken);
            if (auditLogOrNull == null)
            {
                return;
            }

            auditLogOrNull.Status = status;
            auditLogOrNull.ErrorMessage = failureMessage.GetOptionalValue();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private CalendarEventDeletionAuditLogItem createItem(CalendarEventDeletionAuditLog auditLog)
    {
        CalendarEventDeletionAuditLogItem item = new CalendarEventDeletionAuditLogItem();
        item.Id = auditLog.Id;
        item.CalendarId = auditLog.CalendarId;
        item.CalendarName = auditLog.CalendarName;
        item.EventId = auditLog.EventId;
        item.EventSummary = auditLog.EventSummary;
        item.EventLocation = auditLog.EventLocation;
        item.EventStart = auditLog.EventStart;
        item.EventEnd = auditLog.EventEnd;
        item.IsAllDay = auditLog.IsAllDay;
        item.DeletedByDisplayName = auditLog.DeletedByDisplayName;
        item.DeletedByPositionName = auditLog.DeletedByPositionName;
        item.DeletedByTeamName = auditLog.DeletedByTeamName;
        item.DeletedAt = auditLog.DeletedAt;
        item.Status = auditLog.Status;
        item.ErrorMessage = auditLog.ErrorMessage;
        return item;
    }

    private static async Task<UserAccount?> loadUserAccountWithPositionOrNullAsync(
        OrganizationHubDbContext dbContext,
        UserAccountId userAccountId,
        CancellationToken cancellationToken)
    {
        int requiredUserAccountId = userAccountId.GetRequiredValue();
        return await dbContext.UserAccounts
            .AsNoTracking()
            .Include(userAccount => userAccount.Position)
            .FirstOrDefaultAsync(userAccount => userAccount.Id == requiredUserAccountId, cancellationToken);
    }

    private static void assignDeletedByPositionSnapshot(
        CalendarEventDeletionAuditLog auditLog,
        UserAccount? deletedByUserOrNull)
    {
        if (deletedByUserOrNull == null || deletedByUserOrNull.Position == null)
        {
            auditLog.DeletedByPositionId = null;
            auditLog.DeletedByPositionName = string.Empty;
            return;
        }

        auditLog.DeletedByPositionId = deletedByUserOrNull.PositionId;
        auditLog.DeletedByPositionName = deletedByUserOrNull.Position.Name;
    }
}
