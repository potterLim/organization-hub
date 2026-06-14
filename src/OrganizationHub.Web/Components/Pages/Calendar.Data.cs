using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private async Task handleCalendarChangedAsync(string calendarId)
    {
        mSelectedCalendarId = calendarId;
        NavigationManager.NavigateTo("/calendar?calendar=" + Uri.EscapeDataString(calendarId), false);
        await loadEventsAsync();
    }

    private async Task movePreviousMonthAsync()
    {
        mCurrentMonth = mCurrentMonth.AddMonths(-1);
        await loadEventsAsync();
    }

    private async Task moveNextMonthAsync()
    {
        mCurrentMonth = mCurrentMonth.AddMonths(1);
        await loadEventsAsync();
    }

    private Task setCalendarDisplayModeAsync(ECalendarDisplayMode displayMode)
    {
        mIsCalendarMonthView = displayMode == ECalendarDisplayMode.Month;
        return Task.CompletedTask;
    }

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
            ChangeVersionReadResult<IReadOnlyList<CalendarEventItem>> readResult = await ChangeVersionService.ReadWithStableSnapshotAsync(
                changeVersionKeys,
                cancellationToken => getEventsAsync(monthStart, monthEnd, cancellationToken),
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

    private async Task<IReadOnlyList<CalendarEventItem>> getEventsAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        CalendarId selectedCalendarId = CalendarId.CreateOptional(mSelectedCalendarId);
        if (selectedCalendarId.IsAggregate)
        {
            if (CurrentUserSessionOrNull == null)
            {
                return new List<CalendarEventItem>();
            }

            return await CalendarService.GetAggregateEventsAsync(CurrentUserSessionOrNull, start, end, cancellationToken);
        }

        CalendarReference? calendarOrNull = mCalendars.FirstOrDefault(calendar => calendar.CalendarId == selectedCalendarId);
        if (calendarOrNull == null)
        {
            return new List<CalendarEventItem>();
        }

        return await CalendarService.GetEventsAsync(calendarOrNull.CalendarId, calendarOrNull.DisplayName, start, end, cancellationToken);
    }

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

    private CalendarEventItem createOptimisticCalendarEvent(CalendarEventFormModel formModel, string calendarName, UserSession currentUserSession)
    {
        CalendarEventItem calendarEvent = new CalendarEventItem();
        calendarEvent.Id = string.IsNullOrWhiteSpace(formModel.Id) ? "optimistic-" + Guid.NewGuid().ToString("N") : formModel.Id;
        calendarEvent.CalendarId = formModel.CalendarId;
        calendarEvent.CalendarName = calendarName;
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
