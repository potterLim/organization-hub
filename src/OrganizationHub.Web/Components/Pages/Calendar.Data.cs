using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private async Task loadEventsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        mIsLoading = true;
        mErrorMessage = string.Empty;
        try
        {
            DateTime monthStart = mCurrentMonth;
            DateTime today = getClientToday();
            DateTime weekStart = CalendarWeekUtility.GetStartOfWeek(today);
            DateTime weekEnd = CalendarWeekUtility.GetEndOfWeek(today);
            IReadOnlyList<ChangeVersionKey> changeVersionKeys = getChangeVersionKeys();
            CalendarEventDateRange displayDateRange = createCalendarDisplayDateRange(monthStart, weekStart, weekEnd);
            ChangeVersionReadResult<IReadOnlyList<CalendarEventItem>> readResult = await ChangeVersionService.ReadWithStableSnapshotAsync(
                changeVersionKeys,
                cancellationToken => getEventsAsync(displayDateRange, cancellationToken),
                CancellationToken.None);

            mMonthEvents = readResult.Value.OrderBy(calendarEvent => calendarEvent.Start).ThenBy(calendarEvent => calendarEvent.Summary).ToList();
            mWeekEvents = mMonthEvents.Where(calendarEvent => isEventInsideClientDateRange(calendarEvent, weekStart, weekEnd)).ToList();
            applyWritableState(mMonthEvents);
            applyWritableState(mWeekEvents);
            mCalendarDayCells = buildCalendarDayCells(monthStart, mMonthEvents);
            applyInitialDaySelection();
            refreshSelectedEvent();
            EChangeVersionStability stability = readResult.IsStable ? EChangeVersionStability.Stable : EChangeVersionStability.Unstable;
            recordLoadedChangeSnapshot(readResult.Snapshot, stability);
        }
        catch
        {
            mErrorMessage = "일정을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        mIsLoading = false;
    }

    private async Task<IReadOnlyList<CalendarEventItem>> getEventsAsync(CalendarEventDateRange dateRange, CancellationToken cancellationToken)
    {
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null)
        {
            return new List<CalendarEventItem>();
        }

        UserSession currentUserSession = currentUserSessionOrNull;
        CalendarId selectedCalendarId = CalendarId.CreateOptional(mSelectedCalendarId);
        if (selectedCalendarId.IsAggregate)
        {
            return await CalendarService.GetAggregateEventsAsync(currentUserSession, dateRange, cancellationToken);
        }

        CalendarReference? calendarOrNull = mCalendars.FirstOrDefault(calendar => calendar.CalendarId == selectedCalendarId);
        if (calendarOrNull == null)
        {
            return new List<CalendarEventItem>();
        }

        return await CalendarService.GetEventsAsync(currentUserSession, calendarOrNull.CalendarId, CalendarName.CreateOptional(calendarOrNull.DisplayName), dateRange, cancellationToken);
    }

    private CalendarEventDateRange createCalendarDisplayDateRange(DateTime monthStart, DateTime weekStart, DateTime weekEnd)
    {
        DateTime calendarGridStart = monthStart.AddDays(-((int)monthStart.DayOfWeek));
        DateTime calendarGridEnd = calendarGridStart.AddDays(CalendarGridPolicy.VISIBLE_DAY_COUNT);
        DateTime displayStart = calendarGridStart < weekStart ? calendarGridStart : weekStart;
        DateTime displayEnd = calendarGridEnd > weekEnd ? calendarGridEnd : weekEnd;
        return new CalendarEventDateRange(displayStart, displayEnd);
    }
}
