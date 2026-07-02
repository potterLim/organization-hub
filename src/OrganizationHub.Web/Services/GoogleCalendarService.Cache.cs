using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    private async Task<CalendarService> createCalendarServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(CALENDAR_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new CalendarService(initializer);
    }

    private IReadOnlyList<CalendarEventItem>? getCachedCalendarEventsOrNull(string cacheKey)
    {
        IReadOnlyList<CalendarEventItem>? cachedEventsOrNull = mMemoryCache.Get<IReadOnlyList<CalendarEventItem>>(cacheKey);
        if (cachedEventsOrNull == null)
        {
            return null;
        }

        return cloneCalendarEvents(cachedEventsOrNull);
    }

    private void setCalendarEventsCache(string cacheKey, IReadOnlyList<CalendarEventItem> calendarEvents)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = EVENT_LIST_CACHE_DURATION;
        mMemoryCache.Set(cacheKey, cloneCalendarEvents(calendarEvents), cacheEntryOptions);
    }

    private void expireCalendarEventCache(CalendarId calendarId)
    {
        mMemoryCache.Remove(createCalendarVersionCacheKey(calendarId));
        mMemoryCache.Set(createCalendarVersionCacheKey(calendarId), Guid.NewGuid().ToString("N"), CALENDAR_VERSION_CACHE_DURATION);
    }

    private string getCalendarEventCacheVersion(CalendarId calendarId)
    {
        string versionCacheKey = createCalendarVersionCacheKey(calendarId);
        string? versionOrNull = mMemoryCache.Get<string>(versionCacheKey);
        if (versionOrNull != null)
        {
            return versionOrNull;
        }

        string version = Guid.NewGuid().ToString("N");
        mMemoryCache.Set(versionCacheKey, version, CALENDAR_VERSION_CACHE_DURATION);
        return version;
    }

    private string createCalendarVersionCacheKey(CalendarId calendarId)
    {
        return "OrganizationHub:Calendar:Version:" + calendarId.GetRequiredValue();
    }

    private string createEventListCacheKey(CalendarId calendarId, CalendarName calendarName, CalendarEventDateRange dateRange)
    {
        List<string> cacheKeyParts = new List<string>
        {
            "OrganizationHub",
            "Calendar",
            "Events",
            calendarId.GetRequiredValue(),
            getCalendarEventCacheVersion(calendarId),
            calendarName.GetOptionalValue(),
            dateRange.Start.Ticks.ToString(CultureInfo.InvariantCulture),
            dateRange.End.Ticks.ToString(CultureInfo.InvariantCulture)
        };
        return string.Join(":", cacheKeyParts);
    }
}
