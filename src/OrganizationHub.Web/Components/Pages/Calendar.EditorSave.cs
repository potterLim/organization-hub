using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private bool mIsSavingEvent;

    private async Task saveEventAsync()
    {
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null || mIsSavingEvent)
        {
            return;
        }

        UserSession currentUserSession = currentUserSessionOrNull;
        CalendarId editorCalendarId = CalendarId.CreateOptional(mEditorCalendarId);
        IReadOnlyList<CalendarReference> editorCalendars = mIsEditing ? mWritableCalendars : mCreatableCalendars;
        CalendarReference? calendarOrNull = editorCalendars.FirstOrDefault(calendar => calendar.CalendarId == editorCalendarId);
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

        bool isEditing = mIsEditing;
        List<CalendarEventItem> previousMonthEvents = cloneCalendarEvents(mMonthEvents);
        CalendarEventItem? previousSelectedEventOrNull = cloneCalendarEventOrNull(mSelectedEventOrNull);
        try
        {
            CalendarName calendarName = CalendarName.CreateOptional(calendarOrNull.DisplayName);
            mIsSavingEvent = true;
            mEditorMessage = string.Empty;
            CalendarEventItem savedEvent;
            if (isEditing)
            {
                savedEvent = await CalendarService.UpdateEventAsync(currentUserSession, formModelOrNull, calendarName, CancellationToken.None);
            }
            else
            {
                savedEvent = await CalendarService.CreateEventAsync(currentUserSession, formModelOrNull, calendarName, CancellationToken.None);
            }

            applyWritableState(savedEvent);
            closeEditor();
            applySavedEvent(savedEvent);
            recordLoadedChangeSnapshot();
        }
        catch (UnauthorizedAccessException)
        {
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            mEditorMessage = "이 일정을 수정할 권한이 없습니다.";
        }
        catch (ResourceEditConflictException exception)
        {
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            mIsEditorOpen = true;
            mEditorMessage = exception.Message;
            mHasEditConflict = true;
        }
        catch (ResourceCreationException exception)
        {
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            if (exception.IsRemoteStateUncertain)
            {
                mDeletionConflictMessage = exception.Message;
            }
            mIsEditorOpen = exception.IsRemoteStateUncertain == false;
            mEditorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            Logger.LogWarning(exception, "Calendar edit could not be confirmed for {EventId}.", formModelOrNull.Id);
            mEditorMessage = "일정을 저장하지 못했습니다. 작성한 내용은 유지됩니다.";
        }
        finally
        {
            mIsSavingEvent = false;
        }
    }

    private CalendarEventFormModel? createEventFormOrNull()
    {
        if (string.IsNullOrWhiteSpace(mEditorSummary))
        {
            mEditorMessage = "일정 제목을 입력해 주세요.";
            return null;
        }

        if (createEditorScheduleOrNull() is not CalendarEditorSchedule schedule)
        {
            return null;
        }

        CalendarEventFormModel formModel = new CalendarEventFormModel();
        formModel.Id = mEditorEventId;
        formModel.ETag = mEditorETag;
        formModel.CalendarId = mEditorCalendarId;
        formModel.Summary = mEditorSummary;
        formModel.Description = mEditorDescription;
        formModel.Location = mEditorLocation;
        formModel.StartLocal = schedule.StartLocal;
        formModel.EndLocal = schedule.EndLocal;
        formModel.IsAllDay = mIsEditorAllDay;
        return formModel;
    }

    private CalendarEditorSchedule? createEditorScheduleOrNull()
    {
        if (mEditorStartLocalOrNull is not DateTime startLocal || mEditorEndLocalOrNull is not DateTime endLocal)
        {
            mEditorMessage = "시작 일시와 종료 일시를 선택해 주세요.";
            return null;
        }

        DateTime scheduleStartLocal = mIsEditorAllDay ? startLocal.Date : startLocal;
        DateTime scheduleEndLocal = mIsEditorAllDay ? endLocal.Date : endLocal;
        if (scheduleEndLocal < scheduleStartLocal)
        {
            mEditorMessage = mIsEditorAllDay ?
                "종료일은 시작일보다 빠를 수 없습니다." :
                "종료 일시는 시작 일시보다 빠를 수 없습니다.";
            return null;
        }

        return new CalendarEditorSchedule(scheduleStartLocal, scheduleEndLocal);
    }
}
