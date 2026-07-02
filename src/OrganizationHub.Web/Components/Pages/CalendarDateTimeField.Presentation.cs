using System;
using System.Collections.Generic;
using System.Globalization;

namespace OrganizationHub.Web.Components.Pages;

public partial class CalendarDateTimeField
{
    private const string TIME_PART_FORMAT = "00";

    private string getRootClass()
    {
        if (IsDisabled)
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
        if (createSelectionPolicy().getSelectedHour() == hour)
        {
            return "hub-calendar-date-time-slot hub-calendar-date-time-slot-selected";
        }

        return "hub-calendar-date-time-slot";
    }

    private string getMinuteButtonClass(int minute)
    {
        if (createSelectionPolicy().getSelectedMinute() == minute)
        {
            return "hub-calendar-date-time-slot hub-calendar-date-time-slot-selected";
        }

        return "hub-calendar-date-time-slot";
    }

    private string getPanelActionsClass()
    {
        if (CanClearValue)
        {
            return "hub-calendar-date-time-panel-actions hub-calendar-date-time-panel-actions-grouped";
        }

        return "hub-calendar-date-time-panel-actions";
    }

    private string formatTimePart(int value)
    {
        return value.ToString(TIME_PART_FORMAT, CultureInfo.InvariantCulture);
    }
}
