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
        for (int dayIndex = 0; dayIndex < 42; dayIndex++)
        {
            DateTime date = calendarStart.AddDays(dayIndex);
            CalendarDayCell dayCell = new CalendarDayCell();
            dayCell.Date = date;
            dayCell.IsCurrentMonth = date.Month == monthStart.Month;
            dayCell.IsToday = date.Date == DateTime.Today;
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

        return calendarEvent.Start.Value.ToLocalTime().Date;
    }

    private string getCalendarDayCellClass(CalendarDayCell dayCell)
    {
        string className = "hub-calendar-day";
        if (dayCell.IsCurrentMonth == false)
        {
            className += " hub-calendar-day-muted";
        }

        if (dayCell.IsToday)
        {
            className += " hub-calendar-day-today";
        }

        if (dayCell.Events.Count > 0)
        {
            className += " hub-calendar-day-has-events";
        }

        return className;
    }

    private IReadOnlyList<CalendarEventItem> getPreviewDayEvents(CalendarDayCell dayCell)
    {
        return dayCell.Events.Take(4).ToList();
    }

    private int getHiddenDayEventCount(CalendarDayCell dayCell)
    {
        return Math.Max(0, dayCell.Events.Count - getPreviewDayEvents(dayCell).Count);
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

    private string getDayEventCountText(CalendarDayCell dayCell)
    {
        return dayCell.Events.Count.ToString(CultureInfo.InvariantCulture) + "건";
    }

    private string getDaySummaryLabel(CalendarDayCell dayCell)
    {
        return dayCell.Date.ToString("M월 d일", CultureInfo.InvariantCulture) + " 일정 " + getDayEventCountText(dayCell) + " 보기";
    }

    private string getCalendarDayDetailItemClass(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.CanEdit)
        {
            return "hub-calendar-day-detail-item hub-calendar-day-detail-item-editable";
        }

        return "hub-calendar-day-detail-item";
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
            return calendarEvent.Start.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 종일";
        }

        string startText = calendarEvent.Start.Value.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (calendarEvent.End.HasValue == false)
        {
            return startText;
        }

        return startText + " - " + calendarEvent.End.Value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
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

        return calendarEvent.Start.Value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
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
