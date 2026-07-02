using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrganizationHub.Web.Components.Pages;

public partial class CalendarDateTimeField
{
    private Task togglePickerAsync()
    {
        if (IsDisabled)
        {
            return Task.CompletedTask;
        }

        return IsOpenChanged.InvokeAsync(IsOpen == false);
    }

    private Task closePickerAsync()
    {
        return IsOpenChanged.InvokeAsync(false);
    }

    private async Task clearValueAsync()
    {
        if (IsDisabled)
        {
            return;
        }

        await ValueChanged.InvokeAsync(null);
        await closePickerAsync();
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
        CalendarDateTimeSelectionPolicy selectionPolicy = createSelectionPolicy();
        if (selectionPolicy.isDateDisabled(date))
        {
            return;
        }

        DateTime nextValue = selectionPolicy.createSelectedDateValue(date);
        mVisibleMonth = new DateTime(nextValue.Year, nextValue.Month, CalendarGridPolicy.FIRST_DAY_OF_MONTH);
        mDayCells = createDayCells();
        await ValueChanged.InvokeAsync(nextValue);
        if (IsTimeSelectionEnabled == false)
        {
            await closePickerAsync();
        }
    }

    private async Task selectHourAsync(int hour)
    {
        CalendarDateTimeSelectionPolicy selectionPolicy = createSelectionPolicy();
        if (selectionPolicy.isHourDisabled(hour, MINUTE_SLOTS))
        {
            return;
        }

        int minute = selectionPolicy.getSelectedMinute();
        CalendarTimeSlot timeSlot = new CalendarTimeSlot(hour, minute);
        if (selectionPolicy.isDateTimeDisabled(timeSlot))
        {
            minute = selectionPolicy.findFirstEnabledMinute(hour, MINUTE_SLOTS);
            timeSlot = new CalendarTimeSlot(hour, minute);
        }

        await setTimeAsync(timeSlot);
    }

    private async Task selectMinuteAsync(int minute)
    {
        CalendarDateTimeSelectionPolicy selectionPolicy = createSelectionPolicy();
        if (selectionPolicy.isMinuteDisabled(minute))
        {
            return;
        }

        await setTimeAsync(new CalendarTimeSlot(selectionPolicy.getSelectedHour(), minute));
    }

    private async Task setTimeAsync(CalendarTimeSlot timeSlot)
    {
        DateTime nextValue = createSelectionPolicy().createSelectedTimeValue(timeSlot);
        await ValueChanged.InvokeAsync(nextValue);
    }

    private IReadOnlyList<CalendarDateTimeDayCell> createDayCells()
    {
        DateTime monthStart = mVisibleMonth.Date;
        DateTime gridStart = monthStart.AddDays(-(int)monthStart.DayOfWeek);
        CalendarDateTimeSelectionPolicy selectionPolicy = createSelectionPolicy();
        List<CalendarDateTimeDayCell> dayCells = new List<CalendarDateTimeDayCell>();
        for (int index = 0; index < CalendarGridPolicy.VISIBLE_DAY_COUNT; index++)
        {
            DateTime date = gridStart.AddDays(index);
            CalendarDateTimeDayCell dayCell = new CalendarDateTimeDayCell(
                date,
                date.Month == monthStart.Month,
                date.Date == getCurrentDate(),
                Value is DateTime value && value.Date == date.Date,
                selectionPolicy.isDateDisabled(date));
            dayCells.Add(dayCell);
        }

        return dayCells;
    }

    private bool isHourDisabled(int hour)
    {
        return createSelectionPolicy().isHourDisabled(hour, MINUTE_SLOTS);
    }

    private bool isMinuteDisabled(int minute)
    {
        return createSelectionPolicy().isMinuteDisabled(minute);
    }
}
