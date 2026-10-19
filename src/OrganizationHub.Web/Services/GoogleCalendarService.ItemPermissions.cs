using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    private async Task<IReadOnlyList<CalendarEventItem>> applyItemPermissionsAsync(UserSession userSession, IReadOnlyList<CalendarEventItem> events, bool shouldFilterReadAccess, CancellationToken cancellationToken)
    {
        List<CalendarEventItem> visibleEvents = new List<CalendarEventItem>();
        foreach (IGrouping<string, CalendarEventItem> group in events.GroupBy(item => item.CalendarId))
        {
            CalendarId calendarId = CalendarId.CreateOptional(group.Key);
            ResourceOperationAccess update = await mPermissionService.GetCalendarOperationAccessAsync(userSession, calendarId, EResourceOperation.Update, cancellationToken);
            ResourceOperationAccess delete = await mPermissionService.GetCalendarOperationAccessAsync(userSession, calendarId, EResourceOperation.Delete, cancellationToken);
            ResourceOperationAccess read = shouldFilterReadAccess
                ? await mPermissionService.GetCalendarOperationAccessAsync(userSession, calendarId, EResourceOperation.Read, cancellationToken)
                : new ResourceOperationAccess(true, true);
            foreach (CalendarEventItem item in group)
            {
                item.CanEdit = update.CanUse(userSession.UserAccountId, item.CreatedByUserIdOrNull);
                item.CanDelete = delete.CanUse(userSession.UserAccountId, item.CreatedByUserIdOrNull);
                if (read.CanUse(userSession.UserAccountId, item.CreatedByUserIdOrNull))
                {
                    visibleEvents.Add(item);
                }
            }
        }

        return visibleEvents.OrderBy(item => item.Start).ThenBy(item => item.Summary).ToList();
    }

    private async Task ensureCanModifyEventAsync(UserSession userSession, CalendarId calendarId, CalendarEventId eventId, EResourceOperation operation, CancellationToken cancellationToken)
    {
        ResourceOperationAccess access = await mPermissionService.GetCalendarOperationAccessAsync(userSession, calendarId, operation, cancellationToken);
        string requiredCalendarId = calendarId.GetRequiredValue();
        string requiredEventId = eventId.GetRequiredValue();
        int? ownerUserIdOrNull;
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ownerUserIdOrNull = await db.CalendarEventMetadata.Where(item => item.CalendarId == requiredCalendarId && item.EventId == requiredEventId).Select(item => (int?)item.CreatedByUserId).SingleOrDefaultAsync(cancellationToken);
        }

        if (access.CanUse(userSession.UserAccountId, ownerUserIdOrNull) == false)
        {
            throw new UnauthorizedAccessException("이 일정을 변경할 권한이 없습니다.");
        }
    }
}
