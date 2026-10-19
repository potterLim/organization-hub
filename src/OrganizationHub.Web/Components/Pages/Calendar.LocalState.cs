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
        clonedEvent.ETag = calendarEvent.ETag;
        clonedEvent.CalendarId = calendarEvent.CalendarId;
        clonedEvent.CalendarName = calendarEvent.CalendarName;
        clonedEvent.Summary = calendarEvent.Summary;
        clonedEvent.Description = calendarEvent.Description;
        clonedEvent.Location = calendarEvent.Location;
        clonedEvent.Start = calendarEvent.Start;
        clonedEvent.End = calendarEvent.End;
        clonedEvent.IsAllDay = calendarEvent.IsAllDay;
        clonedEvent.CanEdit = calendarEvent.CanEdit;
        clonedEvent.CanDelete = calendarEvent.CanDelete;
        clonedEvent.CreatedByDisplayName = calendarEvent.CreatedByDisplayName;
        clonedEvent.CreatedByUserIdOrNull = calendarEvent.CreatedByUserIdOrNull;
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
        calendarEvent.Start = createClientDateTimeOffset(formModel.StartLocal);
        calendarEvent.End = createClientDateTimeOffset(formModel.IsAllDay ? formModel.EndLocal.Date.AddDays(1) : formModel.EndLocal);
        calendarEvent.IsAllDay = formModel.IsAllDay;
        calendarEvent.CanEdit = true;
        calendarEvent.CanDelete = mIsEditing ? mSelectedEventOrNull != null && mSelectedEventOrNull.CanDelete : mDeletableCalendarIds.Contains(calendarEvent.CalendarId);
        calendarEvent.CreatedByUserIdOrNull = currentUserSession.UserAccountId.GetRequiredValue();
        calendarEvent.CreatedByDisplayName = currentUserSession.DisplayName;
        calendarEvent.CreatedAt = getClientNow();
        return calendarEvent;
    }

    private void applyWritableState(List<CalendarEventItem> events)
    {
        HashSet<string> writableCalendarIds = new HashSet<string>(mWritableCalendars.Select(calendar => calendar.CalendarId.GetRequiredValue()));
        foreach (CalendarEventItem calendarEvent in events)
        {
            calendarEvent.CanEdit = calendarEvent.CanEdit && writableCalendarIds.Contains(calendarEvent.CalendarId);
            calendarEvent.CanDelete = calendarEvent.CanDelete && mDeletableCalendarIds.Contains(calendarEvent.CalendarId);
        }
    }

    private void applySavedEvent(CalendarEventItem calendarEvent)
    {
        removeEventFromMonthList(CalendarId.CreateOptional(calendarEvent.CalendarId), CalendarEventId.CreateOptional(calendarEvent.Id));
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

    private void removeEventFromLocalCollections(CalendarId calendarId, CalendarEventId eventId)
    {
        removeEventFromMonthList(calendarId, eventId);
        refreshDerivedCalendarCollections();
    }

    private void removeEventFromMonthList(CalendarId calendarId, CalendarEventId eventId)
    {
        if (calendarId.HasValue == false || eventId.HasValue == false)
        {
            return;
        }

        string requiredCalendarId = calendarId.GetRequiredValue();
        string requiredEventId = eventId.GetRequiredValue();
        mMonthEvents.RemoveAll(calendarEvent => calendarEvent.CalendarId == requiredCalendarId && calendarEvent.Id == requiredEventId);
    }

    private void refreshDerivedCalendarCollections()
    {
        DateTime today = getClientToday();
        DateTime weekStart = CalendarWeekUtility.GetStartOfWeek(today);
        DateTime weekEnd = CalendarWeekUtility.GetEndOfWeek(today);
        mMonthEvents = mMonthEvents.OrderBy(calendarEvent => calendarEvent.Start).ThenBy(calendarEvent => calendarEvent.Summary).ToList();
        mWeekEvents = mMonthEvents.Where(calendarEvent => isEventInsideClientDateRange(calendarEvent, weekStart, weekEnd)).ToList();
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
        return isEventInsideClientDateRange(calendarEvent, mCurrentMonth.Date, mCurrentMonth.Date.AddMonths(1));
    }

    private bool isEventInsideClientDateRange(CalendarEventItem calendarEvent, DateTime rangeStart, DateTime rangeEnd)
    {
        return CalendarEventPeriod.createOrNull(calendarEvent, mClientTimeZoneInfo)?.overlaps(rangeStart, rangeEnd) == true;
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
