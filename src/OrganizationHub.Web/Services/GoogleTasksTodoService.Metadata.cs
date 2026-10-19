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

public sealed partial class GoogleTasksTodoService
{
    private async Task<Dictionary<string, TodoTaskMetadata>> getMetadataByTaskIdAsync(
        TodoTaskListId taskListId,
        IReadOnlyList<TodoTaskItem> tasks,
        CancellationToken cancellationToken
    )
    {
        List<string> taskIds = tasks.Where(task => string.IsNullOrWhiteSpace(task.Id) == false).Select(task => task.Id).Distinct().ToList();
        if (taskIds.Count == 0)
        {
            return new Dictionary<string, TodoTaskMetadata>();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string requiredTaskListId = taskListId.GetRequiredValue();
            List<TodoTaskMetadata> metadataItems = await dbContext.TodoTaskMetadata.Where(metadata => metadata.TaskListId == requiredTaskListId && taskIds.Contains(metadata.TaskId)).ToListAsync(cancellationToken);
            return metadataItems.ToDictionary(metadata => metadata.TaskId, metadata => metadata);
        }
    }

    private async Task<TodoTaskMetadata?> getMetadataOrNullAsync(TodoTaskListId taskListId, TodoTaskId taskId, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string requiredTaskListId = taskListId.GetRequiredValue();
            string requiredTaskId = taskId.GetRequiredValue();
            TodoTaskMetadata? metadataOrNull = await dbContext.TodoTaskMetadata.FirstOrDefaultAsync(metadata => metadata.TaskListId == requiredTaskListId && metadata.TaskId == requiredTaskId, cancellationToken);
            return metadataOrNull;
        }
    }

    private async Task<TodoTaskMetadata> createMetadataAsync(
        UserSession userSession,
        TodoTaskListId taskListId,
        TodoTaskId taskId,
        DateTime? dueAtOrNull,
        CancellationToken cancellationToken
    )
    {
        string requiredTaskListId = taskListId.GetRequiredValue();
        string requiredTaskId = taskId.GetRequiredValue();
        TodoTaskMetadata metadata = new TodoTaskMetadata();
        metadata.TaskListId = requiredTaskListId;
        metadata.TaskId = requiredTaskId;
        metadata.CreatedByUserId = userSession.UserAccountId.GetRequiredValue();
        metadata.CreatedByDisplayName = userSession.DisplayName;
        metadata.CreatedAt = DateTime.UtcNow;
        metadata.DueAt = dueAtOrNull;

        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                TodoTaskMetadata? existingMetadataOrNull = await dbContext.TodoTaskMetadata.FirstOrDefaultAsync(existingMetadata => existingMetadata.TaskListId == requiredTaskListId && existingMetadata.TaskId == requiredTaskId, cancellationToken);
                if (existingMetadataOrNull == null)
                {
                    dbContext.TodoTaskMetadata.Add(metadata);
                }
                else
                {
                    existingMetadataOrNull.CreatedByUserId = metadata.CreatedByUserId;
                    existingMetadataOrNull.CreatedByDisplayName = metadata.CreatedByDisplayName;
                    existingMetadataOrNull.CreatedAt = metadata.CreatedAt;
                    existingMetadataOrNull.DueAt = metadata.DueAt;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to store Google Tasks metadata for task {TaskId}.", requiredTaskId);
            throw;
        }

        return metadata;
    }

    private async Task deleteMetadataAsync(TodoTaskListId taskListId, TodoTaskId taskId, CancellationToken cancellationToken)
    {
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                string requiredTaskListId = taskListId.GetRequiredValue();
                string requiredTaskId = taskId.GetRequiredValue();
                List<TodoTaskMetadata> metadataItems = await dbContext.TodoTaskMetadata.Where(metadata => metadata.TaskListId == requiredTaskListId && metadata.TaskId == requiredTaskId).ToListAsync(cancellationToken);
                dbContext.TodoTaskMetadata.RemoveRange(metadataItems);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Failed to remove Google Tasks metadata for task {TaskId}.", taskId.GetRequiredValue());
        }
    }

    private void applyMetadata(List<TodoTaskItem> tasks, Dictionary<string, TodoTaskMetadata> metadataByTaskId)
    {
        foreach (TodoTaskItem task in tasks)
        {
            TodoTaskMetadata? metadataOrNull;
            if (metadataByTaskId.TryGetValue(task.Id, out metadataOrNull))
            {
                applyMetadata(task, metadataOrNull);
            }
        }
    }

    private async Task applyMetadataAsync(TodoTaskItem task, CancellationToken cancellationToken)
    {
        TodoTaskMetadata? metadataOrNull = await getMetadataOrNullAsync(TodoTaskListId.CreateOptional(task.TaskListId), TodoTaskId.CreateOptional(task.Id), cancellationToken);
        if (metadataOrNull == null)
        {
            return;
        }

        applyMetadata(task, metadataOrNull);
    }

    private void applyMetadata(TodoTaskItem task, TodoTaskMetadata metadata)
    {
        task.CreatedByDisplayName = metadata.CreatedByDisplayName;
        task.CreatedByUserIdOrNull = metadata.CreatedByUserId > 0 ? metadata.CreatedByUserId : null;
        task.CreatedAt = metadata.CreatedAt == default ? null : metadata.CreatedAt;
        bool hasMatchingVersion = string.IsNullOrEmpty(metadata.GoogleETag) == false && metadata.GoogleETag == task.ETag;
        bool hasMatchingDate = task.DueAt?.Date == metadata.DueAt?.Date;
        if (hasMatchingVersion || hasMatchingDate)
        {
            task.DueAt = normalizeStoredDueAtOrNull(metadata.DueAt);
        }
    }

    private DateTime? normalizeStoredDueAtOrNull(DateTime? dueAtOrNull)
    {
        if (dueAtOrNull.HasValue == false)
        {
            return null;
        }

        return TodoDueAtUtc.CreateRequiredFromStoredValue(dueAtOrNull.Value).GetRequiredValue();
    }
}
