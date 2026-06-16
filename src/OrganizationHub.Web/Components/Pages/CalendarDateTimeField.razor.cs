using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace OrganizationHub.Web.Components.Pages;

public partial class CalendarDateTimeField
{
    private static readonly CultureInfo sKoreanCulture = CultureInfo.GetCultureInfo("ko-KR");
    private static readonly IReadOnlyList<string> sWeekdayLabels = new[] { "일", "월", "화", "수", "목", "금", "토" };
    private static readonly IReadOnlyList<int> sHourSlots = createHourSlots();
    private static readonly IReadOnlyList<int> sMinuteSlots = new[] { 0, 15, 30, 45 };

    private DateTime mVisibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
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
    public bool Disabled
    {
        get; set;
    }

    [Parameter]
    public bool IsTimeSelectionEnabled
    {
        get; set;
    } = true;

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
            mVisibleMonth = getInitialVisibleMonth();
        }

        mDayCells = createDayCells();
    }

    private static IReadOnlyList<int> createHourSlots()
    {
        List<int> hourSlots = new List<int>();
        for (int hour = 0; hour < 24; hour++)
        {
            hourSlots.Add(hour);
        }

        return hourSlots;
    }

    private string getRootClass()
    {
        if (Disabled)
        {
            return "hub-calendar-date-time-field hub-calendar-date-time-field-disabled";
        }

        return "hub-calendar-date-time-field";
    }

    private string getButtonClass()
    {
        if (IsOpen)
        {
            return "hub-calendar-date-time-field-button hub-calendar-date-time-field-button-open";
        }

        return "hub-calendar-date-time-field-button";
    }

    private string getValueText()
    {
        if (Value is not DateTime value)
        {
            return "선택";
        }

        if (IsTimeSelectionEnabled == false)
        {
            return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private Task togglePickerAsync()
    {
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        return IsOpenChanged.InvokeAsync(IsOpen == false);
    }

    private Task closePickerAsync()
    {
        return IsOpenChanged.InvokeAsync(false);
    }

    private Task movePreviousMonthAsync()
    {
        mVisibleMonth = mVisibleMonth.AddMonths(-1);
        mDayCells = createDayCells();
        return Task.CompletedTask;
    }

    private Task moveNextMonthAsync()
    {
        mVisibleMonth = mVisibleMonth.AddMonths(1);
        mDayCells = createDayCells();
        return Task.CompletedTask;
    }

    private async Task selectDateAsync(DateTime date)
    {
        if (isDateDisabled(date))
        {
            return;
        }

        TimeSpan time = IsTimeSelectionEnabled ? getEffectiveSelectedTime() : TimeSpan.Zero;
        DateTime nextValue = date.Date.Add(time);
        if (MinimumValueOrNull is DateTime minimumValue && nextValue < minimumValue)
        {
            nextValue = IsTimeSelectionEnabled ? minimumValue : minimumValue.Date;
        }

        mVisibleMonth = new DateTime(nextValue.Year, nextValue.Month, 1);
        mDayCells = createDayCells();
        await ValueChanged.InvokeAsync(nextValue);
        if (IsTimeSelectionEnabled == false)
        {
            await closePickerAsync();
        }
    }

    private async Task selectHourAsync(int hour)
    {
        if (isHourDisabled(hour))
        {
            return;
        }

        int minute = getSelectedMinute();
        if (isDateTimeDisabled(hour, minute))
        {
            minute = getFirstEnabledMinute(hour);
        }

        await setTimeAsync(hour, minute);
    }

    private async Task selectMinuteAsync(int minute)
    {
        if (isMinuteDisabled(minute))
        {
            return;
        }

        await setTimeAsync(getSelectedHour(), minute);
    }

    private async Task setTimeAsync(int hour, int minute)
    {
        DateTime selectedDate = getEffectiveSelectedDate();
        DateTime nextValue = selectedDate.Date.Add(new TimeSpan(hour, minute, 0));
        await ValueChanged.InvokeAsync(nextValue);
    }

    private DateTime getInitialVisibleMonth()
    {
        DateTime initialDate = getEffectiveSelectedDate();
        return new DateTime(initialDate.Year, initialDate.Month, 1);
    }

    private DateTime getEffectiveSelectedDate()
    {
        if (Value is DateTime value)
        {
            return value.Date;
        }

        if (MinimumValueOrNull is DateTime minimumValue)
        {
            return minimumValue.Date;
        }

        return DateTime.Today;
    }

    private TimeSpan getEffectiveSelectedTime()
    {
        if (Value is DateTime value)
        {
            return value.TimeOfDay;
        }

        if (MinimumValueOrNull is DateTime minimumValue)
        {
            return minimumValue.TimeOfDay;
        }

        return TimeSpan.Zero;
    }

    private int getSelectedHour()
    {
        return getEffectiveSelectedTime().Hours;
    }

    private int getSelectedMinute()
    {
        int minute = getEffectiveSelectedTime().Minutes;
        return minute - minute % 15;
    }

    private IReadOnlyList<CalendarDateTimeDayCell> createDayCells()
    {
        DateTime monthStart = mVisibleMonth.Date;
        DateTime gridStart = monthStart.AddDays(-(int)monthStart.DayOfWeek);
        List<CalendarDateTimeDayCell> dayCells = new List<CalendarDateTimeDayCell>();
        for (int index = 0; index < 42; index++)
        {
            DateTime date = gridStart.AddDays(index);
            CalendarDateTimeDayCell dayCell = new CalendarDateTimeDayCell(
                date,
                date.Month == monthStart.Month,
                date.Date == DateTime.Today,
                Value is DateTime value && value.Date == date.Date,
                isDateDisabled(date));
            dayCells.Add(dayCell);
        }

        return dayCells;
    }

    private bool isDateDisabled(DateTime date)
    {
        return MinimumValueOrNull is DateTime minimumValue && date.Date < minimumValue.Date;
    }

    private bool isHourDisabled(int hour)
    {
        if (IsTimeSelectionEnabled == false)
        {
            return true;
        }

        foreach (int minute in sMinuteSlots)
        {
            if (isDateTimeDisabled(hour, minute) == false)
            {
                return false;
            }
        }

        return true;
    }

    private bool isMinuteDisabled(int minute)
    {
        if (IsTimeSelectionEnabled == false)
        {
            return true;
        }

        return isDateTimeDisabled(getSelectedHour(), minute);
    }

    private bool isDateTimeDisabled(int hour, int minute)
    {
        DateTime selectedDate = getEffectiveSelectedDate();
        DateTime candidate = selectedDate.Date.Add(new TimeSpan(hour, minute, 0));
        return MinimumValueOrNull is DateTime minimumValue && candidate < minimumValue;
    }

    private int getFirstEnabledMinute(int hour)
    {
        foreach (int minute in sMinuteSlots)
        {
            if (isDateTimeDisabled(hour, minute) == false)
            {
                return minute;
            }
        }

        return 0;
    }

    private string getDayButtonClass(CalendarDateTimeDayCell dayCell)
    {
        List<string> classNames = new List<string>();
        classNames.Add("hub-calendar-date-time-day");
        if (dayCell.IsCurrentMonth == false)
        {
            classNames.Add("hub-calendar-date-time-day-muted");
        }

        if (dayCell.IsToday)
        {
            classNames.Add("hub-calendar-date-time-day-today");
        }

        if (dayCell.IsSelected)
        {
            classNames.Add("hub-calendar-date-time-day-selected");
        }

        return string.Join(" ", classNames);
    }

    private string getHourButtonClass(int hour)
    {
        if (getSelectedHour() == hour)
        {
            return "hub-calendar-date-time-slot hub-calendar-date-time-slot-selected";
        }

        return "hub-calendar-date-time-slot";
    }

    private string getMinuteButtonClass(int minute)
    {
        if (getSelectedMinute() == minute)
        {
            return "hub-calendar-date-time-slot hub-calendar-date-time-slot-selected";
        }

        return "hub-calendar-date-time-slot";
    }

    private string formatTimePart(int value)
    {
        return value.ToString("00", CultureInfo.InvariantCulture);
    }

    private readonly record struct CalendarDateTimeDayCell(DateTime Date, bool IsCurrentMonth, bool IsToday, bool IsSelected, bool IsDisabled);
}
