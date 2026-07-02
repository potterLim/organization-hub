using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private string getCalendarViewButtonClass(ECalendarDisplayMode displayMode)
    {
        ECalendarDisplayMode currentDisplayMode = mIsCalendarMonthView ? ECalendarDisplayMode.Month : ECalendarDisplayMode.List;
        if (currentDisplayMode == displayMode)
        {
            return "hub-calendar-view-button hub-calendar-view-button-active";
        }

        return "hub-calendar-view-button";
    }

    private string getCalendarListItemClass(CalendarEventItem calendarEvent)
    {
        if (mSelectedEventOrNull != null && mSelectedEventOrNull.Id == calendarEvent.Id && mSelectedEventOrNull.CalendarId == calendarEvent.CalendarId)
        {
            return "hub-calendar-list-item hub-calendar-list-item-selected";
        }

        return "hub-calendar-list-item";
    }

    private string formatCalendarEventMeta(CalendarEventItem calendarEvent)
    {
        List<string> metadataParts = new List<string>();
        if (shouldShowCalendarName(calendarEvent))
        {
            metadataParts.Add(calendarEvent.CalendarName);
        }

        if (shouldShowCalendarAuthor(calendarEvent))
        {
            metadataParts.Add("작성자: " + calendarEvent.CreatedByDisplayName);
        }

        return string.Join(" · ", metadataParts);
    }

    private string formatCalendarEventLocationMeta(CalendarEventItem calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            return string.Empty;
        }

        return "장소: " + calendarEvent.Location.Trim();
    }

    private bool shouldShowCalendarAuthor(CalendarEventItem calendarEvent)
    {
        return mShouldShowCalendarAuthor && string.IsNullOrWhiteSpace(calendarEvent.CreatedByDisplayName) == false;
    }

    private bool shouldShowCalendarName(CalendarEventItem calendarEvent)
    {
        return shouldShowCalendarName() && string.IsNullOrWhiteSpace(calendarEvent.CalendarName) == false;
    }

    private bool shouldShowCalendarName()
    {
        return CalendarId.CreateOptional(mSelectedCalendarId).IsAggregate;
    }
}
