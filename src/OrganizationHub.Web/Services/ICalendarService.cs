using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEventItem>> GetEventsAsync(CalendarId calendarId, CalendarName calendarName, CalendarEventDateRange dateRange, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarEventItem>> GetTeamEventsAsync(OrganizationTeamId teamId, CalendarEventDateRange dateRange, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarEventItem>> GetAggregateEventsAsync(UserSession userSession, CalendarEventDateRange dateRange, CancellationToken cancellationToken);
    Task<CalendarEventItem> CreateEventAsync(UserSession userSession, CalendarEventFormModel formModel, CalendarName calendarName, CancellationToken cancellationToken);
    Task<CalendarEventItem> UpdateEventAsync(CalendarEventFormModel formModel, CalendarName calendarName, CancellationToken cancellationToken);
    Task DeleteEventAsync(CalendarId calendarId, CalendarEventId eventId, CancellationToken cancellationToken);
}
