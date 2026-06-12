using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEventItem>> GetEventsAsync(string calendarID, string calendarName, DateTime start, DateTime end, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarEventItem>> GetTeamEventsAsync(int teamID, DateTime start, DateTime end, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarEventItem>> GetAggregateEventsAsync(UserSession userSession, DateTime start, DateTime end, CancellationToken cancellationToken);
    Task<CalendarEventItem> CreateEventAsync(UserSession userSession, CalendarEventFormModel formModel, string calendarName, CancellationToken cancellationToken);
    Task<CalendarEventItem> UpdateEventAsync(CalendarEventFormModel formModel, string calendarName, CancellationToken cancellationToken);
    Task DeleteEventAsync(string calendarID, string eventID, CancellationToken cancellationToken);
}
