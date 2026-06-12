using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class GoogleCalendarService : ICalendarService
{
    private const string EVENT_FIELDS = "id,summary,description,location,start,end";
    private static readonly string[] CALENDAR_SCOPES = new[] { CalendarService.Scope.Calendar };
    private static readonly TimeSpan EVENT_LIST_CACHE_DURATION = TimeSpan.FromSeconds(45);

    private readonly IGoogleApiCredentialFactory mCredentialFactory;
    private readonly GoogleApiOptions mOptions;
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IMemoryCache mMemoryCache;
    private readonly ILogger<GoogleCalendarService> mLogger;
    private readonly IChangeVersionService mChangeVersionService;

    public GoogleCalendarService(IGoogleApiCredentialFactory credentialFactory, IOptions<GoogleApiOptions> options, IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IMemoryCache memoryCache, ILogger<GoogleCalendarService> logger, IChangeVersionService changeVersionService)
    {
        mCredentialFactory = credentialFactory;
        mOptions = options.Value;
        mDbContextFactory = dbContextFactory;
        mMemoryCache = memoryCache;
        mLogger = logger;
        mChangeVersionService = changeVersionService;
    }

    public async Task<IReadOnlyList<CalendarEventItem>> GetEventsAsync(string calendarID, string calendarName, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(calendarID))
        {
            return new List<CalendarEventItem>();
        }

        CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
        return await getEventsAsync(calendarService, calendarID, calendarName, start, end, cancellationToken);
    }

    private async Task<IReadOnlyList<CalendarEventItem>> getEventsAsync(CalendarService calendarService, string calendarID, string calendarName, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        string cacheKey = createEventListCacheKey(calendarID, calendarName, start, end);
        IReadOnlyList<CalendarEventItem>? cachedEventsOrNull = getCachedCalendarEventsOrNull(cacheKey);
        if (cachedEventsOrNull != null)
        {
            return cachedEventsOrNull;
        }

        EventsResource.ListRequest request = calendarService.Events.List(calendarID);
        request.TimeMinDateTimeOffset = new DateTimeOffset(start);
        request.TimeMaxDateTimeOffset = new DateTimeOffset(end);
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        request.Fields = "items(" + EVENT_FIELDS + "),nextPageToken";
        Events events = await request.ExecuteAsync(cancellationToken);
        List<CalendarEventItem> calendarEvents = createCalendarEventItems(events, calendarID, calendarName).ToList();
        await applyEventMetadataAsync(calendarID, calendarEvents, cancellationToken);
        setCalendarEventsCache(cacheKey, calendarEvents);
        return cloneCalendarEvents(calendarEvents);
    }

    public async Task<IReadOnlyList<CalendarEventItem>> GetTeamEventsAsync(int teamID, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == teamID && team.IsActive, cancellationToken);
            if (teamOrNull == null || string.IsNullOrWhiteSpace(teamOrNull.CalendarID))
            {
                return new List<CalendarEventItem>();
            }

            return await GetEventsAsync(teamOrNull.CalendarID, teamOrNull.DisplayName, start, end, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<CalendarEventItem>> GetAggregateEventsAsync(UserSession userSession, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        List<CalendarFetchTarget> fetchTargets = new List<CalendarFetchTarget>();
        HashSet<string> fetchedCalendarIDs = new HashSet<string>();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null && userSession.IsManagementRole())
            {
                addCalendarFetchTarget(fetchTargets, fetchedCalendarIDs, settingOrNull.OrganizationCalendarID, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME);
            }

            if (settingOrNull != null && userSession.IsTeamLeaderOrHigherRole())
            {
                addCalendarFetchTarget(fetchTargets, fetchedCalendarIDs, settingOrNull.ResponsibleCalendarID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME);
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarID != null && team.CalendarID != string.Empty).OrderBy(team => team.SortOrder);
            if (userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    return new List<CalendarEventItem>();
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                addCalendarFetchTarget(fetchTargets, fetchedCalendarIDs, team.CalendarID, team.DisplayName);
            }
        }

        if (fetchTargets.Count == 0)
        {
            return new List<CalendarEventItem>();
        }

        CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
        List<Task<IReadOnlyList<CalendarEventItem>>> eventTasks = new List<Task<IReadOnlyList<CalendarEventItem>>>();
        foreach (CalendarFetchTarget fetchTarget in fetchTargets)
        {
            eventTasks.Add(getEventsAsync(calendarService, fetchTarget.CalendarID, fetchTarget.CalendarName, start, end, cancellationToken));
        }

        IReadOnlyList<CalendarEventItem>[] eventGroups = await Task.WhenAll(eventTasks);
        List<CalendarEventItem> mergedEvents = new List<CalendarEventItem>();
        foreach (IReadOnlyList<CalendarEventItem> eventGroup in eventGroups)
        {
            mergedEvents.AddRange(eventGroup);
        }

        List<CalendarEventItem> orderedEvents = mergedEvents.OrderBy(calendarEvent => calendarEvent.Start).ThenBy(calendarEvent => calendarEvent.Summary).ToList();
        return orderedEvents;
    }

    public async Task<CalendarEventItem> CreateEventAsync(UserSession userSession, CalendarEventFormModel formModel, string calendarName, CancellationToken cancellationToken)
    {
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(formModel.CalendarID)))
        {
            CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
            Event googleEvent = createGoogleEvent(formModel);
            EventsResource.InsertRequest request = calendarService.Events.Insert(googleEvent, formModel.CalendarID);
            request.Fields = EVENT_FIELDS;
            Event createdEvent = await request.ExecuteAsync(cancellationToken);
            CalendarEventItem createdItem = createCalendarEventItem(createdEvent, formModel.CalendarID, calendarName);
            CalendarEventMetadata createdMetadata = await createMetadataAsync(userSession, formModel.CalendarID, createdItem.ID, cancellationToken);
            applyMetadata(createdItem, createdMetadata);
            expireCalendarEventCache(formModel.CalendarID);
            return createdItem;
        }
    }

    public async Task<CalendarEventItem> UpdateEventAsync(CalendarEventFormModel formModel, string calendarName, CancellationToken cancellationToken)
    {
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(formModel.CalendarID)))
        {
            CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
            Event googleEvent = createGoogleEvent(formModel);
            EventsResource.PatchRequest request = calendarService.Events.Patch(googleEvent, formModel.CalendarID, formModel.ID);
            request.Fields = EVENT_FIELDS;
            Event updatedEvent = await request.ExecuteAsync(cancellationToken);
            expireCalendarEventCache(formModel.CalendarID);
            return createCalendarEventItem(updatedEvent, formModel.CalendarID, calendarName);
        }
    }

    public async Task DeleteEventAsync(string calendarID, string eventID, CancellationToken cancellationToken)
    {
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(calendarID)))
        {
            CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
            EventsResource.DeleteRequest request = calendarService.Events.Delete(calendarID, eventID);
            await request.ExecuteAsync(cancellationToken);
            await deleteMetadataAsync(calendarID, eventID, cancellationToken);
            expireCalendarEventCache(calendarID);
        }
    }

    private static IReadOnlyList<string> createCalendarChangeKeys(string calendarID)
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.CALENDAR);
        keys.Add(ChangeVersionKeys.CreateCalendarKey(calendarID));
        return keys;
    }

    private Event createGoogleEvent(CalendarEventFormModel formModel)
    {
        Event googleEvent = new Event();
        googleEvent.Summary = formModel.Summary.Trim();
        googleEvent.Description = getEmptyAsNull(formModel.Description);
        googleEvent.Location = getEmptyAsNull(formModel.Location);
        googleEvent.Start = createEventDateTime(formModel, ECalendarEventDateKind.Start);
        googleEvent.End = createEventDateTime(formModel, ECalendarEventDateKind.End);
        return googleEvent;
    }

    private EventDateTime createEventDateTime(CalendarEventFormModel formModel, ECalendarEventDateKind dateKind)
    {
        EventDateTime eventDateTime = new EventDateTime();
        DateTime localDateTime = dateKind == ECalendarEventDateKind.Start ? formModel.StartLocal : formModel.EndLocal;
        if (formModel.IsAllDay)
        {
            DateTime date = localDateTime.Date;
            if (dateKind == ECalendarEventDateKind.End)
            {
                date = date.AddDays(1);
            }

            eventDateTime.Date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return eventDateTime;
        }

        eventDateTime.DateTimeDateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), getCalendarUtcOffset(localDateTime));
        eventDateTime.TimeZone = getCalendarTimeZoneID();
        return eventDateTime;
    }

    private TimeSpan getCalendarUtcOffset(DateTime localDateTime)
    {
        try
        {
            TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(getCalendarTimeZoneID());
            return timeZoneInfo.GetUtcOffset(localDateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        }
    }

    private string getCalendarTimeZoneID()
    {
        if (string.IsNullOrWhiteSpace(mOptions.CalendarTimeZone))
        {
            return "Asia/Seoul";
        }

        return mOptions.CalendarTimeZone;
    }

    private void addCalendarFetchTarget(List<CalendarFetchTarget> fetchTargets, HashSet<string> fetchedCalendarIDs, string? calendarIDOrNull, string calendarName)
    {
        if (string.IsNullOrWhiteSpace(calendarIDOrNull))
        {
            return;
        }

        if (fetchedCalendarIDs.Add(calendarIDOrNull) == false)
        {
            return;
        }

        fetchTargets.Add(new CalendarFetchTarget(calendarIDOrNull, calendarName));
    }

    private async Task applyEventMetadataAsync(string calendarID, List<CalendarEventItem> calendarEvents, CancellationToken cancellationToken)
    {
        Dictionary<string, CalendarEventMetadata> metadataByEventID = await getMetadataByEventIDAsync(calendarID, calendarEvents, cancellationToken);
        foreach (CalendarEventItem calendarEvent in calendarEvents)
        {
            CalendarEventMetadata? metadataOrNull;
            if (metadataByEventID.TryGetValue(calendarEvent.ID, out metadataOrNull))
            {
                applyMetadata(calendarEvent, metadataOrNull);
            }
        }
    }

    private async Task<Dictionary<string, CalendarEventMetadata>> getMetadataByEventIDAsync(string calendarID, IReadOnlyList<CalendarEventItem> calendarEvents, CancellationToken cancellationToken)
    {
        List<string> eventIDs = calendarEvents.Where(calendarEvent => string.IsNullOrWhiteSpace(calendarEvent.ID) == false).Select(calendarEvent => calendarEvent.ID).Distinct().ToList();
        if (eventIDs.Count == 0)
        {
            return new Dictionary<string, CalendarEventMetadata>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<CalendarEventMetadata> metadataItems = await dbContext.CalendarEventMetadata.Where(metadata => metadata.CalendarID == calendarID && eventIDs.Contains(metadata.EventID)).ToListAsync(cancellationToken);
            return metadataItems.ToDictionary(metadata => metadata.EventID, metadata => metadata);
        }
    }

    private async Task<CalendarEventMetadata> createMetadataAsync(UserSession userSession, string calendarID, string eventID, CancellationToken cancellationToken)
    {
        CalendarEventMetadata metadata = new CalendarEventMetadata();
        metadata.CalendarID = calendarID;
        metadata.EventID = eventID;
        metadata.CreatedByUserID = userSession.UserID;
        metadata.CreatedByDisplayName = userSession.DisplayName;
        metadata.CreatedAt = DateTime.UtcNow;

        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                CalendarEventMetadata? existingMetadataOrNull = await dbContext.CalendarEventMetadata.FirstOrDefaultAsync(existingMetadata => existingMetadata.CalendarID == calendarID && existingMetadata.EventID == eventID, cancellationToken);
                if (existingMetadataOrNull == null)
                {
                    dbContext.CalendarEventMetadata.Add(metadata);
                }
                else
                {
                    existingMetadataOrNull.CreatedByUserID = metadata.CreatedByUserID;
                    existingMetadataOrNull.CreatedByDisplayName = metadata.CreatedByDisplayName;
                    existingMetadataOrNull.CreatedAt = metadata.CreatedAt;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to store Google Calendar metadata for event {EventID}.", eventID);
        }

        return metadata;
    }

    private async Task deleteMetadataAsync(string calendarID, string eventID, CancellationToken cancellationToken)
    {
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                List<CalendarEventMetadata> metadataItems = await dbContext.CalendarEventMetadata.Where(metadata => metadata.CalendarID == calendarID && metadata.EventID == eventID).ToListAsync(cancellationToken);
                dbContext.CalendarEventMetadata.RemoveRange(metadataItems);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to remove Google Calendar metadata for event {EventID}.", eventID);
        }
    }

    private void applyMetadata(CalendarEventItem calendarEvent, CalendarEventMetadata metadata)
    {
        calendarEvent.CreatedByDisplayName = metadata.CreatedByDisplayName;
        calendarEvent.CreatedAt = metadata.CreatedAt;
    }

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

    private IReadOnlyList<CalendarEventItem> createCalendarEventItems(Events events, string calendarID, string calendarName)
    {
        List<CalendarEventItem> items = new List<CalendarEventItem>();
        if (events.Items == null)
        {
            return items;
        }

        foreach (Event googleEvent in events.Items)
        {
            CalendarEventItem item = createCalendarEventItem(googleEvent, calendarID, calendarName);
            items.Add(item);
        }

        return items;
    }

    private IReadOnlyList<CalendarEventItem> cloneCalendarEvents(IReadOnlyList<CalendarEventItem> calendarEvents)
    {
        List<CalendarEventItem> clonedEvents = new List<CalendarEventItem>();
        foreach (CalendarEventItem calendarEvent in calendarEvents)
        {
            clonedEvents.Add(cloneCalendarEvent(calendarEvent));
        }

        return clonedEvents;
    }

    private CalendarEventItem cloneCalendarEvent(CalendarEventItem calendarEvent)
    {
        CalendarEventItem clonedEvent = new CalendarEventItem();
        clonedEvent.ID = calendarEvent.ID;
        clonedEvent.CalendarID = calendarEvent.CalendarID;
        clonedEvent.CalendarName = calendarEvent.CalendarName;
        clonedEvent.Summary = calendarEvent.Summary;
        clonedEvent.Description = calendarEvent.Description;
        clonedEvent.Location = calendarEvent.Location;
        clonedEvent.Start = calendarEvent.Start;
        clonedEvent.End = calendarEvent.End;
        clonedEvent.IsAllDay = calendarEvent.IsAllDay;
        clonedEvent.CanEdit = calendarEvent.CanEdit;
        clonedEvent.CreatedByDisplayName = calendarEvent.CreatedByDisplayName;
        clonedEvent.CreatedAt = calendarEvent.CreatedAt;
        return clonedEvent;
    }

    private CalendarEventItem createCalendarEventItem(Event googleEvent, string calendarID, string calendarName)
    {
        CalendarEventItem item = new CalendarEventItem();
        item.ID = getNullAsEmpty(googleEvent.Id);
        item.CalendarID = calendarID;
        item.CalendarName = calendarName;
        item.Summary = getNullAsEmpty(googleEvent.Summary);
        item.Description = getNullAsEmpty(googleEvent.Description);
        item.Location = getNullAsEmpty(googleEvent.Location);
        item.Start = getEventTimeOrNull(googleEvent.Start);
        item.End = getEventTimeOrNull(googleEvent.End);
        item.IsAllDay = googleEvent.Start != null && string.IsNullOrWhiteSpace(googleEvent.Start.Date) == false;
        return item;
    }

    private DateTimeOffset? getEventTimeOrNull(EventDateTime eventDateTimeOrNull)
    {
        if (eventDateTimeOrNull == null)
        {
            return null;
        }

        if (eventDateTimeOrNull.DateTimeDateTimeOffset.HasValue)
        {
            return eventDateTimeOrNull.DateTimeDateTimeOffset.Value;
        }

        if (string.IsNullOrWhiteSpace(eventDateTimeOrNull.Date) == false)
        {
            DateTime dateTime;
            bool isParsed = DateTime.TryParse(eventDateTimeOrNull.Date, out dateTime);
            if (isParsed)
            {
                return new DateTimeOffset(dateTime);
            }
        }

        return null;
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }

    private string? getEmptyAsNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private sealed class CalendarFetchTarget
    {
        public string CalendarID
        {
            get; private init;
        }
        public string CalendarName
        {
            get; private init;
        }

        public CalendarFetchTarget(string calendarID, string calendarName)
        {
            CalendarID = calendarID;
            CalendarName = calendarName;
        }
    }
}
