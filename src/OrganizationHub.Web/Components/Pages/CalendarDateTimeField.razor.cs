using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace OrganizationHub.Web.Components.Pages;

public partial class CalendarDateTimeField
{
    private static readonly CultureInfo KOREAN_CULTURE = CultureInfo.GetCultureInfo("ko-KR");
    private static readonly IReadOnlyList<string> WEEKDAY_LABELS = new string[] { "일", "월", "화", "수", "목", "금", "토" };
    private static readonly IReadOnlyList<int> HOUR_SLOTS = createHourSlots();
    private static readonly IReadOnlyList<int> MINUTE_SLOTS = createMinuteSlots();

    private DateTime mVisibleMonth = createFallbackVisibleMonth();
    private IReadOnlyList<CalendarDateTimeDayCell> mDayCells = new List<CalendarDateTimeDayCell>();

    [Parameter]
    public string Label
    {
        get; set;
    } = string.Empty;

    [Parameter]
    public DateTime? Value
    {
        get; set;
    }

    [Parameter]
    public EventCallback<DateTime?> ValueChanged
    {
        get; set;
    }

    [Parameter]
    public DateTime? MinimumValueOrNull
    {
        get; set;
    }

    [Parameter]
    public DateTime? CurrentDateOrNull
    {
        get; set;
    }

    [Parameter]
    public bool IsDisabled
    {
        get; set;
    }

    [Parameter]
    public bool IsTimeSelectionEnabled
    {
        get; set;
    } = true;

    [Parameter]
    public bool CanClearValue
    {
        get; set;
    }

    [Parameter]
    public string ClearValueText
    {
        get; set;
    } = "선택 없음";

    [Parameter]
    public bool IsOpen
    {
        get; set;
    }

    [Parameter]
    public EventCallback<bool> IsOpenChanged
    {
        get; set;
    }

    protected override void OnParametersSet()
    {
        if (IsOpen == false)
        {
            mVisibleMonth = createSelectionPolicy().getInitialVisibleMonth();
        }

        mDayCells = createDayCells();
    }

    private static IReadOnlyList<int> createHourSlots()
    {
        List<int> hourSlots = new List<int>();
        for (int hour = CalendarTimeSlot.MINIMUM_HOUR_VALUE; hour <= CalendarTimeSlot.MAXIMUM_HOUR_VALUE; hour++)
        {
            hourSlots.Add(hour);
        }

        return hourSlots;
    }

    private static IReadOnlyList<int> createMinuteSlots()
    {
        List<int> minuteSlots = new List<int>();
        for (int minute = CalendarTimeSlot.MINIMUM_MINUTE_VALUE; minute <= CalendarTimeSlot.MAXIMUM_MINUTE_VALUE; minute += CalendarTimeSlot.MINUTE_SELECTION_INTERVAL)
        {
            minuteSlots.Add(minute);
        }

        return minuteSlots;
    }

    private CalendarDateTimeSelectionPolicy createSelectionPolicy()
    {
        return new CalendarDateTimeSelectionPolicy(Value, MinimumValueOrNull, getCurrentDate(), getSelectionMode());
    }

    private ECalendarDateTimeSelectionMode getSelectionMode()
    {
        if (IsTimeSelectionEnabled)
        {
            return ECalendarDateTimeSelectionMode.DateTime;
        }

        return ECalendarDateTimeSelectionMode.DateOnly;
    }

    private DateTime getCurrentDate()
    {
        if (CurrentDateOrNull is DateTime currentDate)
        {
            return currentDate.Date;
        }

        if (Value is DateTime value)
        {
            return value.Date;
        }

        if (MinimumValueOrNull is DateTime minimumValue)
        {
            return minimumValue.Date;
        }

        return DateTime.UtcNow.Date;
    }

    private static DateTime createFallbackVisibleMonth()
    {
        DateTime utcToday = DateTime.UtcNow.Date;
        return new DateTime(utcToday.Year, utcToday.Month, CalendarGridPolicy.FIRST_DAY_OF_MONTH);
    }
}
