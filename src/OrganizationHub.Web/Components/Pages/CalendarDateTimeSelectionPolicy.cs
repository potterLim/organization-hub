using System;
using System.Collections.Generic;

namespace OrganizationHub.Web.Components.Pages;

internal sealed class CalendarDateTimeSelectionPolicy
{
    private readonly DateTime? mValueOrNull;
    private readonly DateTime? mMinimumValueOrNull;
    private readonly DateTime mCurrentDate;
    private readonly ECalendarDateTimeSelectionMode mSelectionMode;

    public CalendarDateTimeSelectionPolicy(
        DateTime? valueOrNull,
        DateTime? minimumValueOrNull,
        DateTime currentDate,
        ECalendarDateTimeSelectionMode selectionMode)
    {
        mValueOrNull = valueOrNull;
        mMinimumValueOrNull = minimumValueOrNull;
        mCurrentDate = currentDate.Date;
        mSelectionMode = selectionMode;
    }

    internal DateTime getInitialVisibleMonth()
    {
        DateTime initialDate = getEffectiveSelectedDate();
        return new DateTime(initialDate.Year, initialDate.Month, CalendarGridPolicy.FIRST_DAY_OF_MONTH);
    }

    internal DateTime getEffectiveSelectedDate()
    {
        if (mValueOrNull is DateTime value)
        {
            return value.Date;
        }

        if (mMinimumValueOrNull is DateTime minimumValue)
        {
            return minimumValue.Date;
        }

        return mCurrentDate;
    }

    internal TimeSpan getEffectiveSelectedTime()
    {
        if (mValueOrNull is DateTime value)
        {
            return value.TimeOfDay;
        }

        if (mMinimumValueOrNull is DateTime minimumValue)
        {
            return minimumValue.TimeOfDay;
        }

        return TimeSpan.Zero;
    }

    internal int getSelectedHour()
    {
        return getEffectiveSelectedTime().Hours;
    }

    internal int getSelectedMinute()
    {
        int minute = getEffectiveSelectedTime().Minutes;
        return minute - minute % CalendarTimeSlot.MINUTE_SELECTION_INTERVAL;
    }

    internal DateTime createSelectedDateValue(DateTime date)
    {
        TimeSpan time = isDateTimeSelectionEnabled() ? getEffectiveSelectedTime() : TimeSpan.Zero;
        DateTime nextValue = date.Date.Add(time);
        if (mMinimumValueOrNull is DateTime minimumValue && nextValue < minimumValue)
        {
            nextValue = isDateTimeSelectionEnabled() ? minimumValue : minimumValue.Date;
        }

        return nextValue;
    }

    internal DateTime createSelectedTimeValue(CalendarTimeSlot timeSlot)
    {
        DateTime selectedDate = getEffectiveSelectedDate();
        return selectedDate.Date.Add(timeSlot.ToTimeSpan());
    }

    internal bool isDateDisabled(DateTime date)
    {
        return mMinimumValueOrNull is DateTime minimumValue && date.Date < minimumValue.Date;
    }

    internal bool isHourDisabled(int hour, IReadOnlyList<int> minuteSlots)
    {
        if (isDateTimeSelectionEnabled() == false)
        {
            return true;
        }

        foreach (int minute in minuteSlots)
        {
            CalendarTimeSlot timeSlot = new CalendarTimeSlot(hour, minute);
            if (isDateTimeDisabled(timeSlot) == false)
            {
                return false;
            }
        }

        return true;
    }

    internal bool isMinuteDisabled(int minute)
    {
        if (isDateTimeSelectionEnabled() == false)
        {
            return true;
        }

        return isDateTimeDisabled(new CalendarTimeSlot(getSelectedHour(), minute));
    }

    internal bool isDateTimeDisabled(CalendarTimeSlot timeSlot)
    {
        DateTime selectedDate = getEffectiveSelectedDate();
        DateTime candidate = selectedDate.Date.Add(timeSlot.ToTimeSpan());
        return mMinimumValueOrNull is DateTime minimumValue && candidate < minimumValue;
    }

    internal int findFirstEnabledMinute(int hour, IReadOnlyList<int> minuteSlots)
    {
        foreach (int minute in minuteSlots)
        {
            CalendarTimeSlot timeSlot = new CalendarTimeSlot(hour, minute);
            if (isDateTimeDisabled(timeSlot) == false)
            {
                return minute;
            }
        }

        return CalendarTimeSlot.MINIMUM_MINUTE_VALUE;
    }

    private bool isDateTimeSelectionEnabled()
    {
        return mSelectionMode == ECalendarDateTimeSelectionMode.DateTime;
    }
}
