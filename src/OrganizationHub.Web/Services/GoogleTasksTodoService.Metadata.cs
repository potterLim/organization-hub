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

public sealed partial class GoogleTasksTodoService
{
    private async Task<Dictionary<string, TodoTaskMetadata>> getMetadataByTaskIDAsync(string taskListID, IReadOnlyList<TodoTaskItem> tasks, CancellationToken cancellationToken)
    {
        List<string> taskIDs = tasks.Where(task => string.IsNullOrWhiteSpace(task.ID) == false).Select(task => task.ID).Distinct().ToList();
        if (taskIDs.Count == 0)
        {
            return new Dictionary<string, TodoTaskMetadata>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<TodoTaskMetadata> metadataItems = await dbContext.TodoTaskMetadata.Where(metadata => metadata.TaskListID == taskListID && taskIDs.Contains(metadata.TaskID)).ToListAsync(cancellationToken);
            return metadataItems.ToDictionary(metadata => metadata.TaskID, metadata => metadata);
        }
    }

    private async Task<TodoTaskMetadata?> getMetadataOrNullAsync(string taskListID, string taskID, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            TodoTaskMetadata? metadataOrNull = await dbContext.TodoTaskMetadata.FirstOrDefaultAsync(metadata => metadata.TaskListID == taskListID && metadata.TaskID == taskID, cancellationToken);
            return metadataOrNull;
        }
    }

    private async Task<TodoTaskMetadata> createMetadataAsync(UserSession userSession, string taskListID, string taskID, CancellationToken cancellationToken)
    {
        TodoTaskMetadata metadata = new TodoTaskMetadata();
        metadata.TaskListID = taskListID;
        metadata.TaskID = taskID;
        metadata.CreatedByUserID = userSession.UserID;
        metadata.CreatedByDisplayName = userSession.DisplayName;
        metadata.CreatedAt = DateTime.UtcNow;

        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                TodoTaskMetadata? existingMetadataOrNull = await dbContext.TodoTaskMetadata.FirstOrDefaultAsync(existingMetadata => existingMetadata.TaskListID == taskListID && existingMetadata.TaskID == taskID, cancellationToken);
                if (existingMetadataOrNull == null)
                {
                    dbContext.TodoTaskMetadata.Add(metadata);
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
            mLogger.LogWarning(exception, "Failed to store Google Tasks metadata for task {TaskID}.", taskID);
        }

        return metadata;
    }

    private async Task deleteMetadataAsync(string taskListID, string taskID, CancellationToken cancellationToken)
    {
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                List<TodoTaskMetadata> metadataItems = await dbContext.TodoTaskMetadata.Where(metadata => metadata.TaskListID == taskListID && metadata.TaskID == taskID).ToListAsync(cancellationToken);
                dbContext.TodoTaskMetadata.RemoveRange(metadataItems);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to remove Google Tasks metadata for task {TaskID}.", taskID);
        }
    }

    private void applyMetadata(List<TodoTaskItem> tasks, Dictionary<string, TodoTaskMetadata> metadataByTaskID)
    {
        foreach (TodoTaskItem task in tasks)
        {
            TodoTaskMetadata? metadataOrNull;
            if (metadataByTaskID.TryGetValue(task.ID, out metadataOrNull))
            {
                applyMetadata(task, metadataOrNull);
            }
        }
    }

    private async Task applyMetadataAsync(TodoTaskItem task, CancellationToken cancellationToken)
    {
        TodoTaskMetadata? metadataOrNull = await getMetadataOrNullAsync(task.TaskListID, task.ID, cancellationToken);
        if (metadataOrNull == null)
        {
            return;
        }

        applyMetadata(task, metadataOrNull);
    }

    private void applyMetadata(TodoTaskItem task, TodoTaskMetadata metadata)
    {
        task.CreatedByDisplayName = metadata.CreatedByDisplayName;
        task.CreatedAt = metadata.CreatedAt;
    }
}
