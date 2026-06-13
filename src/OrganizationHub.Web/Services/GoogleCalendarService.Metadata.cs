using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
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
}
