using System;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private DateTime getClientNow()
    {
        return ClientTimeZoneUtility.getClientNow(mClientTimeZoneInfo);
    }

    private DateTime getClientToday()
    {
        return getClientNow().Date;
    }

    private DateTime getClientMonthStart()
    {
        DateTime today = getClientToday();
        return new DateTime(today.Year, today.Month, CalendarGridPolicy.FIRST_DAY_OF_MONTH);
    }

    private DateTime createClientDateTime(DateTimeOffset dateTimeOffset)
    {
        return ClientTimeZoneUtility.createClientDateTime(dateTimeOffset.UtcDateTime, mClientTimeZoneInfo);
    }

    private DateTimeOffset createClientDateTimeOffset(DateTime localDateTime)
    {
        DateTime unspecifiedLocalDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        TimeSpan utcOffset = mClientTimeZoneInfo.GetUtcOffset(unspecifiedLocalDateTime);
        return new DateTimeOffset(unspecifiedLocalDateTime, utcOffset);
    }

    private async Task initializeClientTimeZoneAsync()
    {
        TimeZoneInfo clientTimeZoneInfo = await ClientTimeZoneService.GetClientTimeZoneAsync(CancellationToken.None);
        if (string.Equals(mClientTimeZoneInfo.Id, clientTimeZoneInfo.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mClientTimeZoneInfo = clientTimeZoneInfo;
        if (mHasAppliedInitialDaySelection == false)
        {
            mCurrentMonth = getClientMonthStart();
        }

        refreshDerivedCalendarCollections();
        await loadEventsAsync();
        await InvokeAsync(StateHasChanged);
    }
}
