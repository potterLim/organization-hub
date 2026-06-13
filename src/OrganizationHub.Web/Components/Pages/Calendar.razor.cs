using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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


    private void applyWritableState(CalendarEventItem calendarEvent)
    {
        calendarEvent.CanEdit = mWritableCalendars.Any(calendar => calendar.CalendarID == calendarEvent.CalendarID);
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

}
