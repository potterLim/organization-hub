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
    private List<CalendarReference> mCreatableCalendars = new List<CalendarReference>();
    private HashSet<string> mDeletableCalendarIds = new HashSet<string>();
    private List<CalendarReference> mWritableCalendars = new List<CalendarReference>();
    private List<CalendarEventItem> mWeekEvents = new List<CalendarEventItem>();
    private List<CalendarEventItem> mMonthEvents = new List<CalendarEventItem>();
    private List<CalendarDayCell> mCalendarDayCells = new List<CalendarDayCell>();
    private readonly string[] mCalendarWeekdayLabels = new string[] { "일", "월", "화", "수", "목", "금", "토" };
    private CalendarEventItem? mDeleteCandidateOrNull;
    private CalendarEventItem? mSelectedEventOrNull;
    private DateTime? mSelectedDayOrNull;
    private string mSelectedCalendarId = "all";
    private string mErrorMessage = string.Empty;
    private string mEditorCalendarId = string.Empty;
    private string mEditorETag = string.Empty;
    private string mEditorEventId = string.Empty;
    private string mEditorSummary = string.Empty;
    private string mEditorDescription = string.Empty;
    private string mEditorLocation = string.Empty;
    private DateTime? mEditorStartLocalOrNull;
    private DateTime? mEditorEndLocalOrNull;
    private string mEditorMessage = string.Empty;
    private TimeZoneInfo mClientTimeZoneInfo = TimeZoneInfo.Local;
    private DateTime mCurrentMonth;
    private ECalendarEditorDateTimeField? mOpenEditorDateTimeFieldOrNull;
    private ElementReference mRefreshScopeElement;
    private ChangeVersionSnapshot mLoadedChangeSnapshot = new ChangeVersionSnapshot();
    private IJSObjectReference? mRefreshModuleOrNull;
    private DotNetObjectReference<Calendar>? mRefreshDotNetReferenceOrNull;
    private bool mIsEditorOpen;
    private bool mIsEditing;
    private bool mIsEditorAllDay;
    private bool mIsLoading;
    private bool mIsCalendarMonthView = true;
    private bool mShouldShowCalendarAuthor = true;
    private bool mHasExternalChanges;
    private bool mHasAppliedInitialDaySelection;

    protected override async Task OnInitializedAsync()
    {
        mCurrentMonth = getClientMonthStart();
        bool hasSession = await tryLoadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        GlobalSettingFormModel globalSetting = await GlobalSettingService.GetGlobalSettingAsync(CancellationToken.None);
        IReadOnlyList<CalendarReference> calendars = await PermissionService.GetReadableCalendarsAsync(CurrentUserSessionOrNull, CancellationToken.None);
        IReadOnlyList<CalendarReference> writableCalendars = await PermissionService.GetWritableCalendarsAsync(CurrentUserSessionOrNull, EResourceOperation.Update, CancellationToken.None);
        mShouldShowCalendarAuthor = globalSetting.ShouldShowCalendarAuthor;
        mCalendars = calendars.ToList();
        mWritableCalendars = writableCalendars.ToList();
        mCreatableCalendars = (await PermissionService.GetWritableCalendarsAsync(CurrentUserSessionOrNull, EResourceOperation.Create, CancellationToken.None)).ToList();
        IReadOnlyList<CalendarReference> deletableCalendars = await PermissionService.GetWritableCalendarsAsync(CurrentUserSessionOrNull, EResourceOperation.Delete, CancellationToken.None);
        mDeletableCalendarIds = deletableCalendars.Select(calendar => calendar.CalendarId.GetRequiredValue()).ToHashSet();
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
        calendarEvent.CanDelete = calendarEvent.CanDelete && mDeletableCalendarIds.Contains(calendarEvent.CalendarId);
        calendarEvent.CanEdit = calendarEvent.CanEdit && mWritableCalendars.Any(calendar => calendar.CalendarId == CalendarId.CreateOptional(calendarEvent.CalendarId));
    }
}
