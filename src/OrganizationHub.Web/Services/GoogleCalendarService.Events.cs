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
}
