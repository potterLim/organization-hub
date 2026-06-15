using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface ICalendarEventDeletionAuditLogService
{
    Task<CalendarEventDeletionAuditLogId> RecordPendingDeletionAsync(UserSession userSession, CalendarEventItem calendarEvent, CancellationToken cancellationToken);
    Task MarkDeletionSucceededAsync(CalendarEventDeletionAuditLogId auditLogId, CancellationToken cancellationToken);
    Task MarkDeletionFailedAsync(CalendarEventDeletionAuditLogId auditLogId, CalendarEventDeletionFailureMessage failureMessage, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarEventDeletionAuditLogItem>> GetRecentDeletedEventsAsync(UserSession userSession, RecentItemLimit maximumCount, CancellationToken cancellationToken);
}
