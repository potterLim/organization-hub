using System;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private async Task handleCalendarChangedAsync(string calendarId)
    {
        mSelectedCalendarId = calendarId;
        NavigationManager.NavigateTo("/calendar?calendar=" + Uri.EscapeDataString(calendarId), false);
        await loadEventsAsync();
    }

    private async Task movePreviousMonthAsync()
    {
        mCurrentMonth = mCurrentMonth.AddMonths(-1);
        await loadEventsAsync();
    }

    private async Task moveNextMonthAsync()
    {
        mCurrentMonth = mCurrentMonth.AddMonths(1);
        await loadEventsAsync();
    }

    private Task setCalendarDisplayModeAsync(ECalendarDisplayMode displayMode)
    {
        mIsCalendarMonthView = displayMode == ECalendarDisplayMode.Month;
        return Task.CompletedTask;
    }
}
