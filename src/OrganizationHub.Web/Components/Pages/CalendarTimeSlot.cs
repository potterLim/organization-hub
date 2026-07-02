using System;
using System.Globalization;

namespace OrganizationHub.Web.Components.Pages;

internal readonly record struct CalendarTimeSlot
{
    public const int MINIMUM_HOUR_VALUE = 0;
    public const int MAXIMUM_HOUR_VALUE = 23;
    public const int MINIMUM_MINUTE_VALUE = 0;
    public const int MAXIMUM_MINUTE_VALUE = 59;
    public const int MINUTE_SELECTION_INTERVAL = 15;

    private const int SECOND_VALUE = 0;

    public int Hour
    {
        get;
    }

    public int Minute
    {
        get;
    }

    public CalendarTimeSlot(int hour, int minute)
    {
        if (hour < MINIMUM_HOUR_VALUE || hour > MAXIMUM_HOUR_VALUE)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between " + MINIMUM_HOUR_VALUE.ToString(CultureInfo.InvariantCulture) + " and " + MAXIMUM_HOUR_VALUE.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (minute < MINIMUM_MINUTE_VALUE || minute > MAXIMUM_MINUTE_VALUE)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between " + MINIMUM_MINUTE_VALUE.ToString(CultureInfo.InvariantCulture) + " and " + MAXIMUM_MINUTE_VALUE.ToString(CultureInfo.InvariantCulture) + ".");
        }

        Hour = hour;
        Minute = minute;
    }

    public TimeSpan ToTimeSpan()
    {
        return new TimeSpan(Hour, Minute, SECOND_VALUE);
    }
}
