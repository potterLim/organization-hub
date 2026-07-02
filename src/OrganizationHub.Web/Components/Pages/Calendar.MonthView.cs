using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private List<CalendarDayCell> buildCalendarDayCells(DateTime monthStart, IReadOnlyList<CalendarEventItem> events)
    {
        DateTime calendarStart = monthStart.AddDays(-((int)monthStart.DayOfWeek));
        Dictionary<DateTime, List<CalendarEventItem>> eventsByDate = new Dictionary<DateTime, List<CalendarEventItem>>();
        foreach (CalendarEventItem calendarEvent in events.OrderBy(calendarEvent => calendarEvent.Start).ThenBy(calendarEvent => calendarEvent.Summary))
        {
            DateTime? eventDateOrNull = getEventLocalDateOrNull(calendarEvent);
            if (eventDateOrNull.HasValue)
            {
                DateTime eventDate = eventDateOrNull.Value.Date;
                List<CalendarEventItem>? dayEventsOrNull;
                if (eventsByDate.TryGetValue(eventDate, out dayEventsOrNull) == false)
                {
                    dayEventsOrNull = new List<CalendarEventItem>();
                    eventsByDate[eventDate] = dayEventsOrNull;
                }

                dayEventsOrNull.Add(calendarEvent);
            }
        }

        List<CalendarDayCell> dayCells = new List<CalendarDayCell>();
        for (int dayIndex = 0; dayIndex < CalendarGridPolicy.VISIBLE_DAY_COUNT; dayIndex++)
        {
            DateTime date = calendarStart.AddDays(dayIndex);
            CalendarDayCell dayCell = new CalendarDayCell();
            dayCell.Date = date;
            dayCell.IsCurrentMonth = date.Month == monthStart.Month;
            dayCell.IsToday = date.Date == getClientToday();
            List<CalendarEventItem>? eventsForDayOrNull;
            if (eventsByDate.TryGetValue(date.Date, out eventsForDayOrNull))
            {
                dayCell.Events.AddRange(eventsForDayOrNull);
            }

            dayCells.Add(dayCell);
        }

        return dayCells;
    }

    private DateTime? getEventLocalDateOrNull(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.Start.HasValue == false)
        {
            return null;
        }

        return createClientDateTime(calendarEvent.Start.Value).Date;
    }

    private IReadOnlyList<CalendarEventItem> getSelectedDayEvents()
    {
        if (mSelectedDayOrNull.HasValue == false)
        {
            return new List<CalendarEventItem>();
        }

        DateTime selectedDay = mSelectedDayOrNull.Value.Date;
        return mMonthEvents
            .Where(calendarEvent => getEventLocalDateOrNull(calendarEvent) == selectedDay)
            .OrderBy(calendarEvent => calendarEvent.Start)
            .ThenBy(calendarEvent => calendarEvent.Summary)
            .ToList();
    }

    private string getCalendarDayDetailItemClass(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.CanEdit)
        {
            return "hub-calendar-day-detail-item hub-calendar-day-detail-item-editable";
        }

        return "hub-calendar-day-detail-item";
    }

    private void applyInitialDaySelection()
    {
        if (mHasAppliedInitialDaySelection)
        {
            return;
        }

        mHasAppliedInitialDaySelection = true;
        if (mSelectedEventOrNull != null || mSelectedDayOrNull.HasValue)
        {
            return;
        }

        DateTime today = getClientToday();
        if (today.Month == mCurrentMonth.Month && today.Year == mCurrentMonth.Year)
        {
            mSelectedDayOrNull = today;
        }
    }

    private void selectDay(CalendarDayCell dayCell)
    {
        mSelectedDayOrNull = dayCell.Date.Date;
        mSelectedEventOrNull = null;
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private void clearSelectedDay()
    {
        mSelectedDayOrNull = null;
        mSelectedEventOrNull = null;
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private string formatEventTime(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.Start.HasValue == false)
        {
            return "시간 미정";
        }

        if (calendarEvent.IsAllDay)
        {
            return createClientDateTime(calendarEvent.Start.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 종일";
        }

        string startText = createClientDateTime(calendarEvent.Start.Value).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (calendarEvent.End.HasValue == false)
        {
            return startText;
        }

        return startText + " - " + createClientDateTime(calendarEvent.End.Value).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatCalendarChipTime(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.Start.HasValue == false)
        {
            return "시간";
        }

        if (calendarEvent.IsAllDay)
        {
            return "종일";
        }

        return createClientDateTime(calendarEvent.Start.Value).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatCalendarDayDetailTime(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.Start.HasValue == false)
        {
            return "시간";
        }

        if (calendarEvent.IsAllDay)
        {
            return "종일";
        }

        DateTime localStart = createClientDateTime(calendarEvent.Start.Value);
        string startText = localStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (calendarEvent.End.HasValue == false)
        {
            return startText;
        }

        DateTime localEnd = createClientDateTime(calendarEvent.End.Value);
        if (localStart.Date == localEnd.Date)
        {
            return startText + " - " + localEnd.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        return localStart.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)
            + " - "
            + localEnd.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatCalendarDayDetailMeta(CalendarEventItem calendarEvent)
    {
        List<string> metadataParts = new List<string>();
        if (shouldShowCalendarName(calendarEvent))
        {
            metadataParts.Add(calendarEvent.CalendarName);
        }

        if (shouldShowCalendarAuthor(calendarEvent))
        {
            metadataParts.Add("작성자: " + calendarEvent.CreatedByDisplayName);
        }

        return string.Join(" · ", metadataParts);
    }

    private string formatCalendarDayDetailLocationMeta(CalendarEventItem calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            return string.Empty;
        }

        return "장소: " + calendarEvent.Location.Trim();
    }

    private string formatSelectedDayTitle()
    {
        if (mSelectedDayOrNull.HasValue == false)
        {
            return "날짜별 일정";
        }

        return mSelectedDayOrNull.Value.ToString("M월 d일 dddd", CultureInfo.GetCultureInfo("ko-KR"));
    }
}
