using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Caching.Memory;
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

    private void expireCalendarEventCache(string calendarID)
    {
        mMemoryCache.Remove(createCalendarVersionCacheKey(calendarID));
        mMemoryCache.Set(createCalendarVersionCacheKey(calendarID), Guid.NewGuid().ToString("N"), TimeSpan.FromHours(1));
    }

    private string getCalendarEventCacheVersion(string calendarID)
    {
        string versionCacheKey = createCalendarVersionCacheKey(calendarID);
        string? versionOrNull = mMemoryCache.Get<string>(versionCacheKey);
        if (versionOrNull != null)
        {
            return versionOrNull;
        }

        string version = Guid.NewGuid().ToString("N");
        mMemoryCache.Set(versionCacheKey, version, TimeSpan.FromHours(1));
        return version;
    }

    private string createCalendarVersionCacheKey(string calendarID)
    {
        return "OrganizationHub:Calendar:Version:" + calendarID;
    }

    private string createEventListCacheKey(string calendarID, string calendarName, DateTime start, DateTime end)
    {
        return "OrganizationHub:Calendar:Events:" + calendarID + ":" + getCalendarEventCacheVersion(calendarID) + ":" + calendarName + ":" + start.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + end.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
