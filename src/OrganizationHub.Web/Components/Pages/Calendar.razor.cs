using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    [SupplyParameterFromQuery(Name = "calendar")]
    public string? CalendarIdOrNull
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
    private string mSelectedCalendarId = "all";
    private string mErrorMessage = string.Empty;
    private string mEditorCalendarId = string.Empty;
    private string mEditorEventId = string.Empty;
    private string mEditorSummary = string.Empty;
    private string mEditorDescription = string.Empty;
    private string mEditorLocation = string.Empty;
    private DateTime? mEditorStartLocalOrNull;
    private DateTime? mEditorEndLocalOrNull;
    private string mEditorMessage = string.Empty;
    private DateTime mCurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private ECalendarEditorDateTimeField? mOpenEditorDateTimeFieldOrNull;
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
        if (string.IsNullOrWhiteSpace(CalendarIdOrNull) == false && mCalendars.Any(calendar => calendar.CalendarId == CalendarId.CreateOptional(CalendarIdOrNull)))
        {
            mSelectedCalendarId = CalendarIdOrNull;
        }
        else if (mCalendars.Count > 0)
        {
            mSelectedCalendarId = mCalendars[0].CalendarId.GetRequiredValue();
        }

        await loadEventsAsync();
    }


    private void applyWritableState(CalendarEventItem calendarEvent)
    {
        calendarEvent.CanEdit = mWritableCalendars.Any(calendar => calendar.CalendarId == CalendarId.CreateOptional(calendarEvent.CalendarId));
    }

}
