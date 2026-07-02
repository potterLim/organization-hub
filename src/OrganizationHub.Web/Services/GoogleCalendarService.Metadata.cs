using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    private async Task applyEventMetadataAsync(CalendarId calendarId, List<CalendarEventItem> calendarEvents, CancellationToken cancellationToken)
    {
        Dictionary<string, CalendarEventMetadata> metadataByEventId = await getMetadataByEventIdAsync(calendarId, calendarEvents, cancellationToken);
        foreach (CalendarEventItem calendarEvent in calendarEvents)
        {
            CalendarEventMetadata? metadataOrNull;
            if (metadataByEventId.TryGetValue(calendarEvent.Id, out metadataOrNull))
            {
                applyMetadata(calendarEvent, metadataOrNull);
            }
        }
    }

    private async Task<Dictionary<string, CalendarEventMetadata>> getMetadataByEventIdAsync(
        CalendarId calendarId,
        IReadOnlyList<CalendarEventItem> calendarEvents,
        CancellationToken cancellationToken
    )
    {
        List<string> eventIds = calendarEvents.Where(calendarEvent => string.IsNullOrWhiteSpace(calendarEvent.Id) == false).Select(calendarEvent => calendarEvent.Id).Distinct().ToList();
        if (eventIds.Count == 0)
        {
            return new Dictionary<string, CalendarEventMetadata>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string requiredCalendarId = calendarId.GetRequiredValue();
            List<CalendarEventMetadata> metadataItems = await dbContext.CalendarEventMetadata.Where(metadata => metadata.CalendarId == requiredCalendarId && eventIds.Contains(metadata.EventId)).ToListAsync(cancellationToken);
            return metadataItems.ToDictionary(metadata => metadata.EventId, metadata => metadata);
        }
    }

    private async Task<CalendarEventMetadata> createMetadataAsync(
        UserSession userSession,
        CalendarId calendarId,
        CalendarEventId eventId,
        CancellationToken cancellationToken
    )
    {
        string requiredCalendarId = calendarId.GetRequiredValue();
        string requiredEventId = eventId.GetRequiredValue();
        CalendarEventMetadata metadata = new CalendarEventMetadata();
        metadata.CalendarId = requiredCalendarId;
        metadata.EventId = requiredEventId;
        metadata.CreatedByUserId = userSession.UserAccountId.GetRequiredValue();
        metadata.CreatedByDisplayName = userSession.DisplayName;
        metadata.CreatedAt = DateTime.UtcNow;

        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                CalendarEventMetadata? existingMetadataOrNull = await dbContext.CalendarEventMetadata.FirstOrDefaultAsync(existingMetadata => existingMetadata.CalendarId == requiredCalendarId && existingMetadata.EventId == requiredEventId, cancellationToken);
                if (existingMetadataOrNull == null)
                {
                    dbContext.CalendarEventMetadata.Add(metadata);
                }
                else
                {
                    existingMetadataOrNull.CreatedByUserId = metadata.CreatedByUserId;
                    existingMetadataOrNull.CreatedByDisplayName = metadata.CreatedByDisplayName;
                    existingMetadataOrNull.CreatedAt = metadata.CreatedAt;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to store Google Calendar metadata for event {EventId}.", requiredEventId);
        }

        return metadata;
    }

    private async Task deleteMetadataAsync(CalendarId calendarId, CalendarEventId eventId, CancellationToken cancellationToken)
    {
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                string requiredCalendarId = calendarId.GetRequiredValue();
                string requiredEventId = eventId.GetRequiredValue();
                List<CalendarEventMetadata> metadataItems = await dbContext.CalendarEventMetadata.Where(metadata => metadata.CalendarId == requiredCalendarId && metadata.EventId == requiredEventId).ToListAsync(cancellationToken);
                dbContext.CalendarEventMetadata.RemoveRange(metadataItems);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to remove Google Calendar metadata for event {EventId}.", eventId.GetRequiredValue());
        }
    }

    private void applyMetadata(CalendarEventItem calendarEvent, CalendarEventMetadata metadata)
    {
        calendarEvent.CreatedByDisplayName = metadata.CreatedByDisplayName;
        calendarEvent.CreatedAt = metadata.CreatedAt;
    }
}
