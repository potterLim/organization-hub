using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    [SupplyParameterFromQuery(Name = "calendar")]
    public string? CalendarIDOrNull
    {
        get; set;
    }

    private List<CalendarReference> mCalendars = new List<CalendarReference>();
    private List<CalendarReference> mWritableCalendars = new List<CalendarReference>();
    private List<CalendarEventItem> mWeekEvents = new List<CalendarEventItem>();
    private List<CalendarEventItem> mMonthEvents = new List<CalendarEventItem>();
    private List<CalendarDayCell> mCalendarDayCells = new List<CalendarDayCell>();
    private readonly string[] mCalendarWeekdayLabels = new[] { "일", "월", "화", "수", "목", "금", "토" };
    private CalendarEventItem? mDeleteCandidateOrNull;
    private CalendarEventItem? mSelectedEventOrNull;
    private DateTime? mSelectedDayOrNull;
    private string mSelectedCalendarID = "all";
    private string mErrorMessage = string.Empty;
    private string mEditorCalendarID = string.Empty;
    private string mEditorEventID = string.Empty;
    private string mEditorSummary = string.Empty;
    private string mEditorDescription = string.Empty;
    private string mEditorLocation = string.Empty;
    private string mEditorStartText = string.Empty;
    private string mEditorEndText = string.Empty;
    private string mEditorMessage = string.Empty;
    private DateTime mCurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private ElementReference mRefreshScopeElement;
    private ChangeVersionSnapshot mLoadedChangeSnapshot = new ChangeVersionSnapshot();
    private IJSObjectReference? mRefreshModuleOrNull;
    private DotNetObjectReference<Calendar>? mRefreshDotNetReferenceOrNull;
    private bool mIsEditorOpen;
    private bool mIsEditing;
    private bool mEditorIsAllDay;
    private bool mIsLoading;
    private bool mIsCalendarMonthView = true;
    private bool mShowCalendarAuthor = true;
    private bool mHasExternalChanges;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        GlobalSettingFormModel globalSetting = await TeamManagementService.GetGlobalSettingAsync(CancellationToken.None);
        IReadOnlyList<CalendarReference> calendars = await PermissionService.GetReadableCalendarsAsync(CurrentUserSessionOrNull, CancellationToken.None);
        IReadOnlyList<CalendarReference> writableCalendars = await PermissionService.GetWritableCalendarsAsync(CurrentUserSessionOrNull, CancellationToken.None);
        mShowCalendarAuthor = globalSetting.ShowCalendarAuthor;
        mCalendars = calendars.ToList();
        mWritableCalendars = writableCalendars.ToList();
        if (string.IsNullOrWhiteSpace(CalendarIDOrNull) == false && mCalendars.Any(calendar => calendar.CalendarID == CalendarIDOrNull))
        {
            mSelectedCalendarID = CalendarIDOrNull;
        }
        else if (mCalendars.Count > 0)
        {
            mSelectedCalendarID = mCalendars[0].CalendarID;
        }

        await loadEventsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender == false)
        {
            return;
        }

        mRefreshModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubChangeRefresh.js");
        mRefreshDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        await mRefreshModuleOrNull.InvokeVoidAsync("initializeChangeRefresh", mRefreshScopeElement, mRefreshDotNetReferenceOrNull);
    }

    private async Task handleCalendarChangedAsync(string calendarID)
    {
        mSelectedCalendarID = calendarID;
        NavigationManager.NavigateTo("/calendar?calendar=" + Uri.EscapeDataString(calendarID), false);
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
            IReadOnlyList<string> changeVersionKeys = getChangeVersionKeys();
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
        if (mSelectedCalendarID == "all")
        {
            if (CurrentUserSessionOrNull == null)
            {
                return new List<CalendarEventItem>();
            }

            return await CalendarService.GetAggregateEventsAsync(CurrentUserSessionOrNull, start, end, cancellationToken);
        }

        CalendarReference? calendarOrNull = mCalendars.FirstOrDefault(calendar => calendar.CalendarID == mSelectedCalendarID);
        if (calendarOrNull == null)
        {
            return new List<CalendarEventItem>();
        }

        return await CalendarService.GetEventsAsync(calendarOrNull.CalendarID, calendarOrNull.DisplayName, start, end, cancellationToken);
    }

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

    private string getCalendarViewButtonClass(ECalendarDisplayMode displayMode)
    {
        ECalendarDisplayMode currentDisplayMode = mIsCalendarMonthView ? ECalendarDisplayMode.Month : ECalendarDisplayMode.List;
        if (currentDisplayMode == displayMode)
        {
            return "hub-calendar-view-button hub-calendar-view-button-active";
        }

        return "hub-calendar-view-button";
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

    private void openCreateEditor()
    {
        DateTime nextStart = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
        if (nextStart <= DateTime.Now)
        {
            nextStart = nextStart.AddHours(1);
        }

        mIsEditorOpen = true;
        mIsEditing = false;
        mSelectedEventOrNull = null;
        mSelectedDayOrNull = null;
        mEditorEventID = string.Empty;
        mEditorSummary = string.Empty;
        mEditorDescription = string.Empty;
        mEditorLocation = string.Empty;
        mEditorStartText = formatEditorDateTime(nextStart);
        mEditorEndText = formatEditorDateTime(nextStart.AddHours(1));
        mEditorIsAllDay = false;
        mEditorMessage = string.Empty;
        mEditorCalendarID = mWritableCalendars.Count > 0 ? mWritableCalendars[0].CalendarID : string.Empty;
    }

    private void openEditEditor(CalendarEventItem calendarEvent)
    {
        mIsEditorOpen = true;
        mIsEditing = true;
        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mEditorEventID = calendarEvent.ID;
        mEditorCalendarID = calendarEvent.CalendarID;
        mEditorSummary = calendarEvent.Summary;
        mEditorDescription = calendarEvent.Description;
        mEditorLocation = calendarEvent.Location;
        mEditorIsAllDay = calendarEvent.IsAllDay;
        mEditorStartText = calendarEvent.Start.HasValue ? formatEditorDateTime(calendarEvent.Start.Value.ToLocalTime().DateTime) : formatEditorDateTime(DateTime.Now);
        mEditorEndText = calendarEvent.End.HasValue ? formatEditorDateTime(calendarEvent.End.Value.ToLocalTime().DateTime) : formatEditorDateTime(DateTime.Now.AddHours(1));
        mEditorMessage = string.Empty;
    }

    private void closeEditor()
    {
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private void selectEvent(CalendarEventItem calendarEvent)
    {
        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private void selectDay(CalendarDayCell dayCell)
    {
        mSelectedDayOrNull = dayCell.Date.Date;
        mSelectedEventOrNull = null;
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private void clearSelectedEvent()
    {
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

    private Task handleEditorCalendarChangedAsync(string calendarID)
    {
        mEditorCalendarID = calendarID;
        return Task.CompletedTask;
    }

    private async Task saveEventAsync()
    {
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null)
        {
            return;
        }

        UserSession currentUserSession = currentUserSessionOrNull;
        CalendarReference? calendarOrNull = mWritableCalendars.FirstOrDefault(calendar => calendar.CalendarID == mEditorCalendarID);
        if (calendarOrNull == null)
        {
            mEditorMessage = "저장할 일정 위치를 선택해 주세요.";
            return;
        }

        CalendarEventFormModel? formModelOrNull = createEventFormOrNull();
        if (formModelOrNull == null)
        {
            return;
        }

        bool canWrite = await PermissionService.CanWriteCalendarAsync(currentUserSession, formModelOrNull.CalendarID, CancellationToken.None);
        if (canWrite == false)
        {
            mEditorMessage = "이 일정 위치를 수정할 권한이 없습니다.";
            return;
        }

        bool isEditing = mIsEditing;
        List<CalendarEventItem> previousMonthEvents = cloneCalendarEvents(mMonthEvents);
        CalendarEventItem? previousSelectedEventOrNull = cloneCalendarEventOrNull(mSelectedEventOrNull);
        try
        {
            CalendarEventItem optimisticEvent = createOptimisticCalendarEvent(formModelOrNull, calendarOrNull.DisplayName, currentUserSession);
            applyWritableState(optimisticEvent);
            mErrorMessage = string.Empty;
            closeEditor();
            applySavedEvent(optimisticEvent);
            await InvokeAsync(StateHasChanged);
            CalendarEventItem savedEvent;
            if (isEditing)
            {
                savedEvent = await CalendarService.UpdateEventAsync(formModelOrNull, calendarOrNull.DisplayName, CancellationToken.None);
            }
            else
            {
                savedEvent = await CalendarService.CreateEventAsync(currentUserSession, formModelOrNull, calendarOrNull.DisplayName, CancellationToken.None);
            }

            applyWritableState(savedEvent);
            if (isEditing == false)
            {
                removeEventFromMonthList(optimisticEvent.CalendarID, optimisticEvent.ID);
            }

            applySavedEvent(savedEvent);
            recordLoadedChangeSnapshot();
        }
        catch
        {
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            mErrorMessage = "일정을 저장하지 못했습니다. 변경 내용은 저장되지 않았습니다. 잠시 후 다시 시도해 주세요.";
        }
    }

    private CalendarEventFormModel? createEventFormOrNull()
    {
        if (string.IsNullOrWhiteSpace(mEditorSummary))
        {
            mEditorMessage = "일정 제목을 입력해 주세요.";
            return null;
        }

        DateTime startLocal;
        DateTime endLocal;
        bool isStartParsed = DateTime.TryParse(mEditorStartText, CultureInfo.CurrentCulture, DateTimeStyles.None, out startLocal);
        bool isEndParsed = DateTime.TryParse(mEditorEndText, CultureInfo.CurrentCulture, DateTimeStyles.None, out endLocal);
        if (isStartParsed == false || isEndParsed == false)
        {
            mEditorMessage = "시작과 종료 시간을 입력해 주세요. 예: 2026-05-22 10:00";
            return null;
        }

        if (mEditorIsAllDay && endLocal.Date < startLocal.Date)
        {
            mEditorMessage = "종일 일정의 종료일은 시작일보다 빠를 수 없습니다.";
            return null;
        }

        if (mEditorIsAllDay == false && endLocal <= startLocal)
        {
            mEditorMessage = "종료 시간은 시작 시간보다 늦어야 합니다.";
            return null;
        }

        CalendarEventFormModel formModel = new CalendarEventFormModel();
        formModel.ID = mEditorEventID;
        formModel.CalendarID = mEditorCalendarID;
        formModel.Summary = mEditorSummary;
        formModel.Description = mEditorDescription;
        formModel.Location = mEditorLocation;
        formModel.StartLocal = startLocal;
        formModel.EndLocal = endLocal;
        formModel.IsAllDay = mEditorIsAllDay;
        return formModel;
    }

    private void requestDelete(CalendarEventItem calendarEvent)
    {
        mDeleteCandidateOrNull = calendarEvent;
    }

    private void cancelDelete()
    {
        mDeleteCandidateOrNull = null;
    }

    private async Task deleteSelectedEventAsync()
    {
        CalendarEventItem? deleteCandidateOrNull = mDeleteCandidateOrNull;
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null || deleteCandidateOrNull == null)
        {
            return;
        }

        CalendarEventItem deleteCandidate = deleteCandidateOrNull;
        UserSession currentUserSession = currentUserSessionOrNull;
        bool canWrite = await PermissionService.CanWriteCalendarAsync(currentUserSession, deleteCandidate.CalendarID, CancellationToken.None);
        if (canWrite == false)
        {
            mErrorMessage = "이 일정을 삭제할 권한이 없습니다.";
            mDeleteCandidateOrNull = null;
            return;
        }

        int auditLogID = 0;
        List<CalendarEventItem> previousMonthEvents = cloneCalendarEvents(mMonthEvents);
        CalendarEventItem? previousSelectedEventOrNull = cloneCalendarEventOrNull(mSelectedEventOrNull);
        mDeleteCandidateOrNull = null;
        mSelectedEventOrNull = null;
        mErrorMessage = string.Empty;
        removeEventFromLocalCollections(deleteCandidate.CalendarID, deleteCandidate.ID);
        await InvokeAsync(StateHasChanged);
        try
        {
            auditLogID = await CalendarEventDeletionAuditLogService.RecordPendingDeletionAsync(currentUserSession, deleteCandidate, CancellationToken.None);
            await CalendarService.DeleteEventAsync(deleteCandidate.CalendarID, deleteCandidate.ID, CancellationToken.None);
        }
        catch (Exception exception)
        {
            if (auditLogID > 0)
            {
                await markDeletionFailedAsync(auditLogID, exception.Message);
            }

            mErrorMessage = "일정을 삭제하지 못했습니다. 일정은 아직 남아 있습니다. 잠시 후 다시 시도해 주세요.";
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            return;
        }

        string completionWarning = string.Empty;
        try
        {
            await CalendarEventDeletionAuditLogService.MarkDeletionSucceededAsync(auditLogID, CancellationToken.None);
            recordLoadedChangeSnapshot();
        }
        catch
        {
            completionWarning = "일정은 삭제됐지만 삭제 기록 상태를 갱신하지 못했습니다. 잠시 후 새로고침해 주세요.";
        }

        if (string.IsNullOrWhiteSpace(completionWarning) == false)
        {
            mErrorMessage = completionWarning;
        }
    }

    private async Task markDeletionFailedAsync(int auditLogID, string errorMessage)
    {
        try
        {
            await CalendarEventDeletionAuditLogService.MarkDeletionFailedAsync(auditLogID, errorMessage, CancellationToken.None);
        }
        catch
        {
        }
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
        clonedEvent.ID = calendarEvent.ID;
        clonedEvent.CalendarID = calendarEvent.CalendarID;
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
        calendarEvent.ID = string.IsNullOrWhiteSpace(formModel.ID) ? "optimistic-" + Guid.NewGuid().ToString("N") : formModel.ID;
        calendarEvent.CalendarID = formModel.CalendarID;
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
        HashSet<string> writableCalendarIDs = new HashSet<string>(mWritableCalendars.Select(calendar => calendar.CalendarID));
        foreach (CalendarEventItem calendarEvent in events)
        {
            calendarEvent.CanEdit = writableCalendarIDs.Contains(calendarEvent.CalendarID);
        }
    }

    private void applyWritableState(CalendarEventItem calendarEvent)
    {
        calendarEvent.CanEdit = mWritableCalendars.Any(calendar => calendar.CalendarID == calendarEvent.CalendarID);
    }

    private void applySavedEvent(CalendarEventItem calendarEvent)
    {
        removeEventFromMonthList(calendarEvent.CalendarID, calendarEvent.ID);
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

    private void removeEventFromLocalCollections(string calendarID, string eventID)
    {
        removeEventFromMonthList(calendarID, eventID);
        refreshDerivedCalendarCollections();
    }

    private void removeEventFromMonthList(string calendarID, string eventID)
    {
        mMonthEvents.RemoveAll(calendarEvent => calendarEvent.CalendarID == calendarID && calendarEvent.ID == eventID);
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

        if (mSelectedCalendarID == "all")
        {
            return true;
        }

        return calendarEvent.CalendarID == mSelectedCalendarID;
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

        CalendarEventItem? selectedEventOrNull = mMonthEvents.FirstOrDefault(calendarEvent => calendarEvent.ID == mSelectedEventOrNull.ID && calendarEvent.CalendarID == mSelectedEventOrNull.CalendarID);
        mSelectedEventOrNull = selectedEventOrNull;
    }

    private string getCalendarListItemClass(CalendarEventItem calendarEvent)
    {
        if (mSelectedEventOrNull != null && mSelectedEventOrNull.ID == calendarEvent.ID && mSelectedEventOrNull.CalendarID == calendarEvent.CalendarID)
        {
            return "hub-calendar-list-item hub-calendar-list-item-selected";
        }

        return "hub-calendar-list-item";
    }

    private string getCalendarMobileCardClass(CalendarEventItem calendarEvent)
    {
        if (mSelectedEventOrNull != null && mSelectedEventOrNull.ID == calendarEvent.ID && mSelectedEventOrNull.CalendarID == calendarEvent.CalendarID)
        {
            return "hub-calendar-mobile-card hub-calendar-mobile-card-selected";
        }

        return "hub-calendar-mobile-card";
    }

    private string formatEditorDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
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

    private string formatCalendarEventMeta(CalendarEventItem calendarEvent)
    {
        List<string> metadataParts = new List<string>();
        metadataParts.Add(calendarEvent.CalendarName);
        if (shouldShowCalendarAuthor(calendarEvent))
        {
            metadataParts.Add("작성자: " + calendarEvent.CreatedByDisplayName);
        }

        metadataParts.Add(formatEventTime(calendarEvent));
        return string.Join(" · ", metadataParts);
    }

    [JSInvokable("CheckForExternalChanges")]
    public async Task CheckForExternalChangesAsync()
    {
        if (mIsLoading || CurrentUserSessionOrNull == null)
        {
            return;
        }

        if (ChangeVersionService.HasChanged(mLoadedChangeSnapshot) == false)
        {
            return;
        }

        if (mIsEditorOpen || mDeleteCandidateOrNull != null)
        {
            mHasExternalChanges = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        await loadEventsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void recordLoadedChangeSnapshot(ChangeVersionSnapshot? loadedSnapshotOrNull = null, EChangeVersionStability stability = EChangeVersionStability.Stable)
    {
        if (loadedSnapshotOrNull == null)
        {
            mLoadedChangeSnapshot = ChangeVersionService.GetSnapshot(getChangeVersionKeys());
        }
        else
        {
            mLoadedChangeSnapshot = loadedSnapshotOrNull;
        }

        mHasExternalChanges = stability == EChangeVersionStability.Unstable;
    }

    private IReadOnlyList<string> getChangeVersionKeys()
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.CALENDAR);
        if (mSelectedCalendarID != "all")
        {
            keys.Add(ChangeVersionKeys.CreateCalendarKey(mSelectedCalendarID));
        }

        return keys;
    }

    private bool shouldShowCalendarAuthor(CalendarEventItem calendarEvent)
    {
        return mShowCalendarAuthor && string.IsNullOrWhiteSpace(calendarEvent.CreatedByDisplayName) == false;
    }

    private sealed class CalendarDayCell
    {
        public DateTime Date
        {
            get; set;
        }
        public bool IsCurrentMonth
        {
            get; set;
        }
        public bool IsToday
        {
            get; set;
        }
        public List<CalendarEventItem> Events { get; set; } = new List<CalendarEventItem>();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (mRefreshModuleOrNull != null)
            {
                await mRefreshModuleOrNull.InvokeVoidAsync("disposeChangeRefresh", mRefreshScopeElement);
                await mRefreshModuleOrNull.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        if (mRefreshDotNetReferenceOrNull != null)
        {
            mRefreshDotNetReferenceOrNull.Dispose();
        }
    }
}
