using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    public async Task<IReadOnlyList<CalendarEventItem>> GetEventsAsync(
        CalendarId calendarId,
        CalendarName calendarName,
        CalendarEventDateRange dateRange,
        CancellationToken cancellationToken)
    {
        if (calendarId.HasValue == false)
        {
            return new List<CalendarEventItem>();
        }

        CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
        CalendarEventFetchRequest request = new CalendarEventFetchRequest(calendarService, calendarId, calendarName, dateRange);
        return await getEventsAsync(request, cancellationToken);
    }

    private async Task<IReadOnlyList<CalendarEventItem>> getEventsAsync(CalendarEventFetchRequest request, CancellationToken cancellationToken)
    {
        string cacheKey = createEventListCacheKey(request.CalendarId, request.CalendarName, request.DateRange);
        IReadOnlyList<CalendarEventItem>? cachedEventsOrNull = getCachedCalendarEventsOrNull(cacheKey);
        if (cachedEventsOrNull != null)
        {
            return cachedEventsOrNull;
        }

        string requiredCalendarId = request.CalendarId.GetRequiredValue();
        EventsResource.ListRequest listRequest = request.CalendarService.Events.List(requiredCalendarId);
        listRequest.TimeMinDateTimeOffset = createCalendarDateTimeOffset(request.DateRange.Start);
        listRequest.TimeMaxDateTimeOffset = createCalendarDateTimeOffset(request.DateRange.End);
        listRequest.SingleEvents = true;
        listRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        listRequest.Fields = "items(" + EVENT_FIELDS + "),nextPageToken";
        Events events = await listRequest.ExecuteAsync(cancellationToken);
        List<CalendarEventItem> calendarEvents = createCalendarEventItems(events, request.CalendarId, request.CalendarName).ToList();
        await applyEventMetadataAsync(request.CalendarId, calendarEvents, cancellationToken);
        setCalendarEventsCache(cacheKey, calendarEvents);
        return cloneCalendarEvents(calendarEvents);
    }

    public async Task<IReadOnlyList<CalendarEventItem>> GetTeamEventsAsync(OrganizationTeamId teamId, CalendarEventDateRange dateRange, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredTeamId = teamId.GetRequiredValue();
            Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.Id == requiredTeamId && team.IsActive, cancellationToken);
            if (teamOrNull == null || string.IsNullOrWhiteSpace(teamOrNull.CalendarId))
            {
                return new List<CalendarEventItem>();
            }

            return await GetEventsAsync(CalendarId.CreateOptional(teamOrNull.CalendarId), CalendarName.CreateOptional(teamOrNull.DisplayName), dateRange, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<CalendarEventItem>> GetAggregateEventsAsync(
        UserSession userSession,
        CalendarEventDateRange dateRange,
        CancellationToken cancellationToken
    )
    {
        List<CalendarFetchTarget> fetchTargets = new List<CalendarFetchTarget>();
        HashSet<string> fetchedCalendarIds = new HashSet<string>();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null && userSession.IsManagementRole())
            {
                addCalendarFetchTarget(fetchTargets, fetchedCalendarIds, settingOrNull.OrganizationCalendarId, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME);
            }

            if (settingOrNull != null && userSession.IsTeamLeaderOrHigherRole())
            {
                addCalendarFetchTarget(fetchTargets, fetchedCalendarIds, settingOrNull.ResponsibleCalendarId, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME);
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarId != null && team.CalendarId != string.Empty).OrderBy(team => team.SortOrder);
            if (userSession.IsManagementRole() == false)
            {
                if (userSession.HasTeam == false)
                {
                    return new List<CalendarEventItem>();
                }

                int teamId = userSession.GetRequiredTeamIdValue();
                teamQuery = teamQuery.Where(team => team.Id == teamId);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                addCalendarFetchTarget(fetchTargets, fetchedCalendarIds, team.CalendarId, team.DisplayName);
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
            CalendarEventFetchRequest request = new CalendarEventFetchRequest(calendarService, fetchTarget.CalendarId, fetchTarget.CalendarName, dateRange);
            eventTasks.Add(getEventsAsync(request, cancellationToken));
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

    public async Task<CalendarEventItem> CreateEventAsync(
        UserSession userSession,
        CalendarEventFormModel formModel,
        CalendarName calendarName,
        CancellationToken cancellationToken
    )
    {
        CalendarId calendarId = formModel.CalendarIdValue;
        string requiredCalendarId = calendarId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(calendarId)))
        {
            CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
            Event googleEvent = createGoogleEvent(formModel);
            EventsResource.InsertRequest request = calendarService.Events.Insert(googleEvent, requiredCalendarId);
            request.Fields = EVENT_FIELDS;
            Event createdEvent = await request.ExecuteAsync(cancellationToken);
            CalendarEventItem createdItem = createCalendarEventItem(createdEvent, calendarId, calendarName);
            CalendarEventMetadata createdMetadata = await createMetadataAsync(userSession, calendarId, CalendarEventId.CreateOptional(createdItem.Id), cancellationToken);
            applyMetadata(createdItem, createdMetadata);
            expireCalendarEventCache(calendarId);
            return createdItem;
        }
    }

    public async Task<CalendarEventItem> UpdateEventAsync(CalendarEventFormModel formModel, CalendarName calendarName, CancellationToken cancellationToken)
    {
        CalendarId calendarId = formModel.CalendarIdValue;
        CalendarEventId eventId = formModel.EventIdValue;
        string requiredCalendarId = calendarId.GetRequiredValue();
        string requiredEventId = eventId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(calendarId)))
        {
            CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
            Event googleEvent = createGoogleEvent(formModel);
            EventsResource.PatchRequest request = calendarService.Events.Patch(googleEvent, requiredCalendarId, requiredEventId);
            request.Fields = EVENT_FIELDS;
            Event updatedEvent = await request.ExecuteAsync(cancellationToken);
            expireCalendarEventCache(calendarId);
            return createCalendarEventItem(updatedEvent, calendarId, calendarName);
        }
    }

    public async Task DeleteEventAsync(CalendarId calendarId, CalendarEventId eventId, CancellationToken cancellationToken)
    {
        string requiredCalendarId = calendarId.GetRequiredValue();
        string requiredEventId = eventId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(calendarId)))
        {
            CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
            EventsResource.DeleteRequest request = calendarService.Events.Delete(requiredCalendarId, requiredEventId);
            await request.ExecuteAsync(cancellationToken);
            await deleteMetadataAsync(calendarId, eventId, cancellationToken);
            expireCalendarEventCache(calendarId);
        }
    }

    private sealed class CalendarEventFetchRequest
    {
        public CalendarEventFetchRequest(
            CalendarService calendarService,
            CalendarId calendarId,
            CalendarName calendarName,
            CalendarEventDateRange dateRange)
        {
            CalendarService = calendarService;
            CalendarId = calendarId;
            CalendarName = calendarName;
            DateRange = dateRange;
        }

        public CalendarService CalendarService
        {
            get; private init;
        }

        public CalendarId CalendarId
        {
            get; private init;
        }

        public CalendarName CalendarName
        {
            get; private init;
        }

        public CalendarEventDateRange DateRange
        {
            get; private init;
        }
    }
}
