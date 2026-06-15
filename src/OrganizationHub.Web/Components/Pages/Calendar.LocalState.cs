using System;
using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private List<CalendarEventItem> cloneCalendarEvents(List<CalendarEventItem> calendarEvents)
    {
        return calendarEvents.Select(calendarEvent => cloneCalendarEvent(calendarEvent)).ToList();
    }

    private CalendarEventItem? cloneCalendarEventOrNull(CalendarEventItem? calendarEventOrNull)
    {
        if (calendarEventOrNull == null)
        {
            return null;
        }

        return cloneCalendarEvent(calendarEventOrNull);
    }

    private CalendarEventItem cloneCalendarEvent(CalendarEventItem calendarEvent)
    {
        CalendarEventItem clonedEvent = new CalendarEventItem();
        clonedEvent.Id = calendarEvent.Id;
        clonedEvent.CalendarId = calendarEvent.CalendarId;
        clonedEvent.CalendarName = calendarEvent.CalendarName;
        clonedEvent.Summary = calendarEvent.Summary;
        clonedEvent.Description = calendarEvent.Description;
        clonedEvent.Location = calendarEvent.Location;
        clonedEvent.Start = calendarEvent.Start;
        clonedEvent.End = calendarEvent.End;
        clonedEvent.IsAllDay = calendarEvent.IsAllDay;
        clonedEvent.CanEdit = calendarEvent.CanEdit;
        clonedEvent.CreatedByDisplayName = calendarEvent.CreatedByDisplayName;
        clonedEvent.CreatedAt = calendarEvent.CreatedAt;
        return clonedEvent;
    }

    private CalendarEventItem createOptimisticCalendarEvent(CalendarEventFormModel formModel, CalendarName calendarName, UserSession currentUserSession)
    {
        CalendarEventItem calendarEvent = new CalendarEventItem();
        calendarEvent.Id = string.IsNullOrWhiteSpace(formModel.Id) ? "optimistic-" + Guid.NewGuid().ToString("N") : formModel.Id;
        calendarEvent.CalendarId = formModel.CalendarId;
        calendarEvent.CalendarName = calendarName.GetOptionalValue();
        calendarEvent.Summary = formModel.Summary;
        calendarEvent.Description = formModel.Description;
        calendarEvent.Location = formModel.Location;
        calendarEvent.Start = new DateTimeOffset(formModel.StartLocal);
        calendarEvent.End = new DateTimeOffset(formModel.EndLocal);
        calendarEvent.IsAllDay = formModel.IsAllDay;
        calendarEvent.CanEdit = true;
        calendarEvent.CreatedByDisplayName = currentUserSession.DisplayName;
        calendarEvent.CreatedAt = DateTime.Now;
        return calendarEvent;
    }

    private void applyWritableState(List<CalendarEventItem> events)
    {
        HashSet<string> writableCalendarIds = new HashSet<string>(mWritableCalendars.Select(calendar => calendar.CalendarId.GetRequiredValue()));
        foreach (CalendarEventItem calendarEvent in events)
        {
            calendarEvent.CanEdit = writableCalendarIds.Contains(calendarEvent.CalendarId);
        }
    }

    private void applySavedEvent(CalendarEventItem calendarEvent)
    {
        removeEventFromMonthList(calendarEvent.CalendarId, calendarEvent.Id);
        if (isVisibleInCurrentSelection(calendarEvent))
        {
            mMonthEvents.Add(calendarEvent);
        }

        refreshDerivedCalendarCollections();
        if (isVisibleInCurrentSelection(calendarEvent))
        {
            mSelectedEventOrNull = calendarEvent;
        }
    }

    private void removeEventFromLocalCollections(string calendarId, string eventId)
    {
        removeEventFromMonthList(calendarId, eventId);
        refreshDerivedCalendarCollections();
    }

    private void removeEventFromMonthList(string calendarId, string eventId)
    {
        mMonthEvents.RemoveAll(calendarEvent => calendarEvent.CalendarId == calendarId && calendarEvent.Id == eventId);
    }

    private void refreshDerivedCalendarCollections()
    {
        DateTime today = DateTime.Today;
        DateTime weekStart = CalendarWeekUtility.GetStartOfWeek(today);
        DateTime weekEnd = CalendarWeekUtility.GetEndOfWeek(today);
        mMonthEvents = mMonthEvents.OrderBy(calendarEvent => calendarEvent.Start).ThenBy(calendarEvent => calendarEvent.Summary).ToList();
        mWeekEvents = mMonthEvents.Where(calendarEvent => calendarEvent.Start.HasValue && calendarEvent.Start.Value.Date >= weekStart && calendarEvent.Start.Value.Date < weekEnd).ToList();
        applyWritableState(mMonthEvents);
        applyWritableState(mWeekEvents);
        mCalendarDayCells = buildCalendarDayCells(mCurrentMonth, mMonthEvents);
        refreshSelectedEvent();
    }

    private bool isVisibleInCurrentSelection(CalendarEventItem calendarEvent)
    {
        if (isEventInsideCurrentMonth(calendarEvent) == false)
        {
            return false;
        }

        if (CalendarId.CreateOptional(mSelectedCalendarId).IsAggregate)
        {
            return true;
        }

        return calendarEvent.CalendarId == mSelectedCalendarId;
    }

    private bool isEventInsideCurrentMonth(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.Start.HasValue == false)
        {
            return false;
        }

        DateTime monthStart = mCurrentMonth.Date;
        DateTime monthEnd = monthStart.AddMonths(1);
        DateTime eventStart = calendarEvent.Start.Value.ToLocalTime().Date;
        DateTime eventEnd = eventStart;
        if (calendarEvent.End.HasValue)
        {
            eventEnd = calendarEvent.End.Value.ToLocalTime().Date;
        }

        return eventStart < monthEnd && eventEnd >= monthStart;
    }

    private void refreshSelectedEvent()
    {
        if (mSelectedEventOrNull == null)
        {
            return;
        }

        CalendarEventItem? selectedEventOrNull = mMonthEvents.FirstOrDefault(calendarEvent => calendarEvent.Id == mSelectedEventOrNull.Id && calendarEvent.CalendarId == mSelectedEventOrNull.CalendarId);
        mSelectedEventOrNull = selectedEventOrNull;
    }
}
