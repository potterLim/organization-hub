using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface ICalendarEventDeletionAuditLogService
{
    Task<int> RecordPendingDeletionAsync(UserSession userSession, CalendarEventItem calendarEvent, CancellationToken cancellationToken);
    Task MarkDeletionSucceededAsync(int auditLogID, CancellationToken cancellationToken);
    Task MarkDeletionFailedAsync(int auditLogID, string errorMessage, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarEventDeletionAuditLogItem>> GetRecentDeletedEventsAsync(UserSession userSession, int maximumCount, CancellationToken cancellationToken);
}
