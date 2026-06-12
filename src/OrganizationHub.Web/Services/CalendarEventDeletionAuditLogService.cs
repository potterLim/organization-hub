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

    public CalendarEventDeletionAuditLogService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<int> RecordPendingDeletionAsync(UserSession userSession, CalendarEventItem calendarEvent, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            CalendarEventDeletionAuditLog auditLog = new CalendarEventDeletionAuditLog();
            auditLog.CalendarID = calendarEvent.CalendarID;
            auditLog.CalendarName = calendarEvent.CalendarName;
            auditLog.EventID = calendarEvent.ID;
            auditLog.EventSummary = calendarEvent.Summary;
            auditLog.EventLocation = calendarEvent.Location;
            auditLog.EventStart = calendarEvent.Start;
            auditLog.EventEnd = calendarEvent.End;
            auditLog.IsAllDay = calendarEvent.IsAllDay;
            auditLog.DeletedByUserID = userSession.UserID;
            auditLog.DeletedByDisplayName = userSession.DisplayName;
            auditLog.DeletedByRole = userSession.Role;
            auditLog.DeletedByTeamID = userSession.TeamID;
            auditLog.DeletedByTeamName = userSession.TeamDisplayName;
            auditLog.DeletedAt = DateTime.UtcNow;
            auditLog.Status = ECalendarEventDeletionAuditStatus.Pending;
            dbContext.CalendarEventDeletionAuditLogs.Add(auditLog);
            await dbContext.SaveChangesAsync(cancellationToken);
            return auditLog.ID;
        }
    }

    public async Task MarkDeletionSucceededAsync(int auditLogID, CancellationToken cancellationToken)
    {
        await updateStatusAsync(auditLogID, ECalendarEventDeletionAuditStatus.Succeeded, string.Empty, cancellationToken);
    }

    public async Task MarkDeletionFailedAsync(int auditLogID, string errorMessage, CancellationToken cancellationToken)
    {
        await updateStatusAsync(auditLogID, ECalendarEventDeletionAuditStatus.Failed, errorMessage, cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEventDeletionAuditLogItem>> GetRecentDeletedEventsAsync(UserSession userSession, int maximumCount, CancellationToken cancellationToken)
    {
        if (userSession.IsManagementRole() == false)
        {
            return new List<CalendarEventDeletionAuditLogItem>();
        }

        int takeCount = Math.Clamp(maximumCount, 1, 200);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<CalendarEventDeletionAuditLog> auditLogs = await dbContext.CalendarEventDeletionAuditLogs.OrderByDescending(auditLog => auditLog.DeletedAt).ThenByDescending(auditLog => auditLog.ID).Take(takeCount).ToListAsync(cancellationToken);
            List<CalendarEventDeletionAuditLogItem> items = new List<CalendarEventDeletionAuditLogItem>();
            foreach (CalendarEventDeletionAuditLog auditLog in auditLogs)
            {
                items.Add(createItem(auditLog));
            }

            return items;
        }
    }

    private async Task updateStatusAsync(int auditLogID, ECalendarEventDeletionAuditStatus status, string errorMessage, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            CalendarEventDeletionAuditLog? auditLogOrNull = await dbContext.CalendarEventDeletionAuditLogs.FirstOrDefaultAsync(auditLog => auditLog.ID == auditLogID, cancellationToken);
            if (auditLogOrNull == null)
            {
                return;
            }

            auditLogOrNull.Status = status;
            auditLogOrNull.ErrorMessage = errorMessage;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private CalendarEventDeletionAuditLogItem createItem(CalendarEventDeletionAuditLog auditLog)
    {
        CalendarEventDeletionAuditLogItem item = new CalendarEventDeletionAuditLogItem();
        item.ID = auditLog.ID;
        item.CalendarID = auditLog.CalendarID;
        item.CalendarName = auditLog.CalendarName;
        item.EventID = auditLog.EventID;
        item.EventSummary = auditLog.EventSummary;
        item.EventLocation = auditLog.EventLocation;
        item.EventStart = auditLog.EventStart;
        item.EventEnd = auditLog.EventEnd;
        item.IsAllDay = auditLog.IsAllDay;
        item.DeletedByDisplayName = auditLog.DeletedByDisplayName;
        item.DeletedByRole = auditLog.DeletedByRole;
        item.DeletedByTeamName = auditLog.DeletedByTeamName;
        item.DeletedAt = auditLog.DeletedAt;
        item.Status = auditLog.Status;
        item.ErrorMessage = auditLog.ErrorMessage;
        return item;
    }
}
