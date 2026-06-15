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
            DateTime monthEnd = monthStart.AddMonths(1);
            IReadOnlyList<ChangeVersionKey> changeVersionKeys = getChangeVersionKeys();
            CalendarEventDateRange monthDateRange = new CalendarEventDateRange(monthStart, monthEnd);
            ChangeVersionReadResult<IReadOnlyList<CalendarEventItem>> readResult = await ChangeVersionService.ReadWithStableSnapshotAsync(
                changeVersionKeys,
                cancellationToken => getEventsAsync(monthDateRange, cancellationToken),
                CancellationToken.None);

            DateTime today = DateTime.Today;
            DateTime weekStart = CalendarWeekUtility.GetStartOfWeek(today);
            DateTime weekEnd = CalendarWeekUtility.GetEndOfWeek(today);
            mMonthEvents = readResult.Value.OrderBy(calendarEvent => calendarEvent.Start).ThenBy(calendarEvent => calendarEvent.Summary).ToList();
            mWeekEvents = mMonthEvents.Where(calendarEvent => calendarEvent.Start.HasValue && calendarEvent.Start.Value.Date >= weekStart && calendarEvent.Start.Value.Date < weekEnd).ToList();
            applyWritableState(mMonthEvents);
            applyWritableState(mWeekEvents);
            mCalendarDayCells = buildCalendarDayCells(monthStart, mMonthEvents);
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
        CalendarId selectedCalendarId = CalendarId.CreateOptional(mSelectedCalendarId);
        if (selectedCalendarId.IsAggregate)
        {
            if (CurrentUserSessionOrNull == null)
            {
                return new List<CalendarEventItem>();
            }

            return await CalendarService.GetAggregateEventsAsync(CurrentUserSessionOrNull, dateRange, cancellationToken);
        }

        CalendarReference? calendarOrNull = mCalendars.FirstOrDefault(calendar => calendar.CalendarId == selectedCalendarId);
        if (calendarOrNull == null)
        {
            return new List<CalendarEventItem>();
        }

        return await CalendarService.GetEventsAsync(calendarOrNull.CalendarId, CalendarName.CreateOptional(calendarOrNull.DisplayName), dateRange, cancellationToken);
    }
}
