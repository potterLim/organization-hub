using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private async Task saveEventAsync()
    {
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null)
        {
            return;
        }

        UserSession currentUserSession = currentUserSessionOrNull;
        CalendarId editorCalendarId = CalendarId.CreateOptional(mEditorCalendarId);
        CalendarReference? calendarOrNull = mWritableCalendars.FirstOrDefault(calendar => calendar.CalendarId == editorCalendarId);
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

        bool canWrite = await PermissionService.CanWriteCalendarAsync(currentUserSession, CalendarId.CreateOptional(formModelOrNull.CalendarId), CancellationToken.None);
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
            CalendarName calendarName = CalendarName.CreateOptional(calendarOrNull.DisplayName);
            CalendarEventItem optimisticEvent = createOptimisticCalendarEvent(formModelOrNull, calendarName, currentUserSession);
            applyWritableState(optimisticEvent);
            mErrorMessage = string.Empty;
            closeEditor();
            applySavedEvent(optimisticEvent);
            await InvokeAsync(StateHasChanged);
            CalendarEventItem savedEvent;
            if (isEditing)
            {
                savedEvent = await CalendarService.UpdateEventAsync(formModelOrNull, calendarName, CancellationToken.None);
            }
            else
            {
                savedEvent = await CalendarService.CreateEventAsync(currentUserSession, formModelOrNull, calendarName, CancellationToken.None);
            }

            applyWritableState(savedEvent);
            if (isEditing == false)
            {
                removeEventFromMonthList(optimisticEvent.CalendarId, optimisticEvent.Id);
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
        formModel.Id = mEditorEventId;
        formModel.CalendarId = mEditorCalendarId;
        formModel.Summary = mEditorSummary;
        formModel.Description = mEditorDescription;
        formModel.Location = mEditorLocation;
        formModel.StartLocal = startLocal;
        formModel.EndLocal = endLocal;
        formModel.IsAllDay = mEditorIsAllDay;
        return formModel;
    }
}
