using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    public async Task<IReadOnlyList<CalendarEventItem>> GetEventsAsync(
        UserSession userSession,
        CalendarId calendarId,
        CalendarName calendarName,
        CalendarEventDateRange dateRange,
        CancellationToken cancellationToken)
    {
        await ensureCanReadCalendarAsync(userSession, calendarId, cancellationToken);
        IReadOnlyList<CalendarEventItem> events = await getEventsByCalendarAsync(calendarId, calendarName, dateRange, cancellationToken);
        return await applyItemPermissionsAsync(userSession, events, true, cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEventItem>> GetAggregateEventsAsync(
        UserSession userSession,
        CalendarEventDateRange dateRange,
        CancellationToken cancellationToken
    )
    {
        List<CalendarFetchTarget> fetchTargets = new List<CalendarFetchTarget>();
        HashSet<string> fetchedCalendarIds = new HashSet<string>();
        IReadOnlyList<CalendarReference> readableCalendars = await mPermissionService.GetReadableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference readableCalendar in readableCalendars)
        {
            if (readableCalendar.CalendarId.IsAggregate)
            {
                continue;
            }

            addCalendarFetchTarget(fetchTargets, fetchedCalendarIds, readableCalendar.CalendarId.GetRequiredValue(), readableCalendar.DisplayName);
        }

        if (fetchTargets.Count == 0)
        {
            return new List<CalendarEventItem>();
        }

        using (CalendarService calendarService = await createCalendarServiceAsync(cancellationToken))
        {
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
            return await applyItemPermissionsAsync(userSession, orderedEvents, true, cancellationToken);
        }
    }

    public async Task<CalendarEventItem> CreateEventAsync(
        UserSession userSession,
        CalendarEventFormModel formModel,
        CalendarName calendarName,
        CancellationToken cancellationToken
    )
    {
        CalendarId calendarId = formModel.CalendarIdValue;
        await ensureCanWriteCalendarAsync(userSession, calendarId, EResourceOperation.Create, cancellationToken);
        string requiredCalendarId = calendarId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(calendarId)))
        {
            using (CalendarService calendarService = await createCalendarServiceAsync(cancellationToken))
            {
                Event googleEvent = createGoogleEvent(formModel, calendarName);
                EventsResource.InsertRequest request = calendarService.Events.Insert(googleEvent, requiredCalendarId);
                request.Fields = EVENT_FIELDS;
                Event createdEvent = await request.ExecuteAsync(cancellationToken);
                CalendarEventItem createdItem = createCalendarEventItem(createdEvent, calendarId, calendarName);
                CalendarEventMetadata createdMetadata = await ResourceCreationGuard.PersistMetadataAsync(
                    () => createMetadataAsync(userSession, calendarId, CalendarEventId.CreateOptional(createdItem.Id), cancellationToken),
                    rollbackToken => calendarService.Events.Delete(requiredCalendarId, createdItem.Id).ExecuteAsync(rollbackToken),
                    requiredCalendarId + "/" + createdItem.Id, mLogger);
                applyMetadata(createdItem, createdMetadata);
                expireCalendarEventCache(calendarId);
                await applyItemPermissionsAsync(userSession, new CalendarEventItem[] { createdItem }, false, cancellationToken);
                return createdItem;
            }
        }
    }

    public async Task<CalendarEventItem> UpdateEventAsync(
        UserSession userSession,
        CalendarEventFormModel formModel,
        CalendarName calendarName,
        CancellationToken cancellationToken
    )
    {
        CalendarId calendarId = formModel.CalendarIdValue;
        CalendarEventId eventId = formModel.EventIdValue;
        await ensureCanModifyEventAsync(userSession, calendarId, CalendarEventId.CreateOptional(formModel.Id), EResourceOperation.Update, cancellationToken);
        string requiredCalendarId = calendarId.GetRequiredValue();
        string requiredEventId = eventId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(calendarId)))
        {
            using (CalendarService calendarService = await createCalendarServiceAsync(cancellationToken))
            {
                Event googleEvent = createGoogleEvent(formModel, calendarName);
                EventsResource.PatchRequest request = calendarService.Events.Patch(googleEvent, requiredCalendarId, requiredEventId);
                request.Fields = EVENT_FIELDS;
                request.ModifyRequest = message => ResourceEditVersion.Apply(message, formModel.ETag);
                Event updatedEvent = await ResourceEditVersion.ExecuteAsync(() => request.ExecuteAsync(cancellationToken));
                expireCalendarEventCache(calendarId);
                CalendarEventItem updatedItem = createCalendarEventItem(updatedEvent, calendarId, calendarName);
                Dictionary<string, CalendarEventMetadata> metadataById = await getMetadataByEventIdAsync(calendarId, new List<CalendarEventItem> { updatedItem }, cancellationToken);
                if (metadataById.TryGetValue(updatedItem.Id, out CalendarEventMetadata? metadataOrNull))
                {
                    applyMetadata(updatedItem, metadataOrNull);
                }
                await applyItemPermissionsAsync(userSession, new CalendarEventItem[] { updatedItem }, false, cancellationToken);
                return updatedItem;
            }
        }
    }

    public async Task DeleteEventAsync(UserSession userSession, CalendarId calendarId, CalendarEventId eventId, CancellationToken cancellationToken)
    {
        await ensureCanModifyEventAsync(userSession, calendarId, eventId, EResourceOperation.Delete, cancellationToken);
        string requiredCalendarId = calendarId.GetRequiredValue();
        string requiredEventId = eventId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createCalendarChangeKeys(calendarId)))
        {
            using (CalendarService calendarService = await createCalendarServiceAsync(cancellationToken))
            {
                EventsResource.DeleteRequest request = calendarService.Events.Delete(requiredCalendarId, requiredEventId);
                await request.ExecuteAsync(cancellationToken);
                await deleteMetadataAsync(calendarId, eventId, cancellationToken);
                expireCalendarEventCache(calendarId);
            }
        }
    }


    private async Task<IReadOnlyList<CalendarEventItem>> getEventsByCalendarAsync(
        CalendarId calendarId,
        CalendarName calendarName,
        CalendarEventDateRange dateRange,
        CancellationToken cancellationToken)
    {
        if (calendarId.HasValue == false)
        {
            return new List<CalendarEventItem>();
        }

        using (CalendarService calendarService = await createCalendarServiceAsync(cancellationToken))
        {
            CalendarEventFetchRequest request = new CalendarEventFetchRequest(calendarService, calendarId, calendarName, dateRange);
            return await getEventsAsync(request, cancellationToken);
        }
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
        List<CalendarEventItem> calendarEvents = new List<CalendarEventItem>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events events = await listRequest.ExecuteAsync(cancellationToken);
            calendarEvents.AddRange(createCalendarEventItems(events, request.CalendarId, request.CalendarName));
            listRequest.PageToken = events.NextPageToken;
        }
        while (string.IsNullOrEmpty(listRequest.PageToken) == false);

        await applyEventMetadataAsync(request.CalendarId, calendarEvents, cancellationToken);
        setCalendarEventsCache(cacheKey, calendarEvents);
        return cloneCalendarEvents(calendarEvents);
    }
    private async Task ensureCanWriteCalendarAsync(UserSession userSession, CalendarId calendarId, EResourceOperation operation, CancellationToken cancellationToken)
    {
        bool canWrite = await mPermissionService.CanWriteCalendarAsync(userSession, calendarId, operation, cancellationToken);
        if (canWrite == false)
        {
            throw new UnauthorizedAccessException("The current user cannot write to the selected calendar.");
        }
    }

    private async Task ensureCanReadCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
    {
        bool canRead = await mPermissionService.CanReadCalendarAsync(userSession, calendarId, cancellationToken);
        if (canRead == false)
        {
            throw new UnauthorizedAccessException("The current user cannot read the selected calendar.");
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
