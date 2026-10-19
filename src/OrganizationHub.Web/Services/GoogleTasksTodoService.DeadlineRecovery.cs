using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTask = Google.Apis.Tasks.v1.Data.Task;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleTasksTodoService
{
    private const int DEADLINE_RECOVERY_BATCH_SIZE = 20;
    private static readonly TimeSpan ABANDONED_DEADLINE_CHANGE_AGE = TimeSpan.FromMinutes(5);

    public async Task RecoverDeadlineChangesAsync(CancellationToken cancellationToken)
    {
        List<TodoDeadlineChange> changes;
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            changes = await db.TodoDeadlineChanges.AsNoTracking().OrderBy(change => change.CreatedAt).Take(DEADLINE_RECOVERY_BATCH_SIZE).ToListAsync(cancellationToken);
        }
        if (changes.Count == 0)
        {
            return;
        }
        using (GoogleTasksApi service = await createTasksServiceAsync(cancellationToken))
        {
            foreach (TodoDeadlineChange change in changes)
            {
                try
                {
                    Google.Apis.Tasks.v1.TasksResource.GetRequest request = service.Tasks.Get(change.TaskListId, change.TaskId);
                    request.Fields = TASK_FIELDS;
                    GoogleTask remote = await request.ExecuteAsync(cancellationToken);
                    TodoTaskItem item = createTaskItem(TodoTaskListId.CreateOptional(change.TaskListId), remote);
                    if (matchesDeadlineChange(change, item))
                    {
                        await finishDeadlineChangeAsync(change, item.ETag, cancellationToken);
                    }
                    else if (item.ETag != change.ExpectedETag || change.CreatedAt < DateTime.UtcNow.Subtract(ABANDONED_DEADLINE_CHANGE_AGE))
                    {
                        await removeDeadlineChangeAsync(change.Id, cancellationToken);
                    }
                }
                catch (Google.GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    await removeDeadlineChangeAsync(change.Id, cancellationToken);
                }
                catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
                {
                    mLogger.LogWarning(exception, "Deadline recovery {ChangeId} will be retried.", change.Id);
                }
            }
        }
    }

    private async Task<TodoDeadlineChange> prepareDeadlineChangeAsync(TodoTaskFormModel form, CancellationToken cancellationToken)
    {
        TodoDeadlineChange change = new TodoDeadlineChange();
        change.TaskId = form.TaskId;
        change.TaskListId = form.TaskListId;
        change.ExpectedETag = form.ETag;
        change.Title = form.Title.Trim();
        change.DueAt = form.DueAtUtc;
        change.CreatedAt = DateTime.UtcNow;
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            db.TodoDeadlineChanges.Add(change);
            await db.SaveChangesAsync(cancellationToken);
        }
        return change;
    }

    private async Task finishDeadlineChangeAsync(TodoDeadlineChange change, string googleETag, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            TodoDeadlineChange? pendingOrNull = await db.TodoDeadlineChanges.FindAsync(new object[] { change.Id }, cancellationToken);
            if (pendingOrNull == null)
            {
                return;
            }
            TodoTaskMetadata? metadataOrNull = await db.TodoTaskMetadata.SingleOrDefaultAsync(metadata => metadata.TaskListId == change.TaskListId && metadata.TaskId == change.TaskId, cancellationToken);
            if (metadataOrNull == null)
            {
                metadataOrNull = new TodoTaskMetadata();
                metadataOrNull.TaskListId = change.TaskListId;
                metadataOrNull.TaskId = change.TaskId;
                db.TodoTaskMetadata.Add(metadataOrNull);
            }
            // Google ETags are opaque and also change on completion. Persist operation order independently.
            if (metadataOrNull.LastDeadlineChangeId < change.Id)
            {
                metadataOrNull.DueAt = change.DueAt;
                metadataOrNull.GoogleETag = googleETag;
                metadataOrNull.LastDeadlineChangeId = change.Id;
            }
            db.TodoDeadlineChanges.Remove(pendingOrNull);
            await db.SaveChangesAsync(cancellationToken);
        }
        mChangeVersionService.Bump(ChangeVersionKeys.TASKS);
    }

    private async Task applyPendingDeadlinesAsync(TodoTaskListId listId, List<TodoTaskItem> tasks, IReadOnlyDictionary<string, TodoTaskMetadata> metadataByTaskId, CancellationToken cancellationToken)
    {
        string requiredListId = listId.GetRequiredValue();
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<TodoDeadlineChange> changes = await db.TodoDeadlineChanges.AsNoTracking().Where(change => change.TaskListId == requiredListId).OrderByDescending(change => change.Id).ToListAsync(cancellationToken);
            foreach (TodoTaskItem task in tasks)
            {
                TodoTaskMetadata? metadataOrNull;
                int appliedChangeId = metadataByTaskId.TryGetValue(task.Id, out metadataOrNull) ? metadataOrNull.LastDeadlineChangeId : 0;
                TodoDeadlineChange? changeOrNull = changes.FirstOrDefault(change => change.Id > appliedChangeId && change.TaskId == task.Id && matchesDeadlineChange(change, task));
                if (changeOrNull != null)
                {
                    task.DueAt = changeOrNull.DueAt;
                }
            }
        }
    }

    private async Task removeDeadlineChangeAsync(int id, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await db.TodoDeadlineChanges.Where(change => change.Id == id).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static bool matchesDeadlineChange(TodoDeadlineChange change, TodoTaskItem task)
    {
        return string.IsNullOrEmpty(task.ETag) == false && task.ETag != change.ExpectedETag && task.Title == change.Title && task.DueAt?.Date == change.DueAt?.Date;
    }
}
