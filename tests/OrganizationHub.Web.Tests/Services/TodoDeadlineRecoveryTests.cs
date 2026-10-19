using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TodoDeadlineRecoveryTests
{
    private sealed class StorageFailure : SaveChangesInterceptor
    {
        public bool ShouldFail
        {
            get; set;
        }
        public bool ShouldFailIntent
        {
            get; set;
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (ShouldFailIntent && data.Context!.ChangeTracker.Entries<TodoDeadlineChange>().Any(entry => entry.State == EntityState.Added))
            {
                throw new IOException("Intent storage failed before Google.");
            }
            if (ShouldFail && data.Context!.ChangeTracker.Entries<TodoTaskMetadata>().Any(entry => entry.State == EntityState.Modified))
            {
                throw new IOException("Storage failed after the Google write.");
            }
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PartialDeadlineSaveRemainsRecoverableAfterServiceRestartWithoutRepeatingGoogleWriteAsync(bool shouldClear)
    {
        StorageFailure failure = new StorageFailure();
        using (TestDbContextFactory raw = new TestDbContextFactory(failure))
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.TODOS_UPDATE, raw))
        {
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                TodoTaskMetadata owner = new TodoTaskMetadata();
                owner.TaskId = "item";
                owner.TaskListId = "tasks";
                owner.CreatedByUserId = 1;
                owner.DueAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
                owner.GoogleETag = transport.ETag;
                db.TodoTaskMetadata.Add(owner);
                await db.SaveChangesAsync();
            }
            TodoTaskFormModel form = new TodoTaskFormModel();
            form.TaskId = "item";
            form.TaskListId = "tasks";
            form.Title = "Updated";
            form.ETag = transport.ETag;
            form.DueAtUtc = shouldClear ? null : new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
            failure.ShouldFail = true;
            await Assert.ThrowsAsync<ResourceSynchronizationException>(() => createService(factory, transport).UpdateTaskAsync(OperationPermissionRegressionTests.createSession(), form, CancellationToken.None));
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Single(db.TodoDeadlineChanges);
            }
            failure.ShouldFail = false;
            await createService(factory, transport).RecoverDeadlineChangesAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.TodoDeadlineChanges);
                Assert.Equal(form.DueAtUtc, db.TodoTaskMetadata.Single().DueAt);
                Assert.Equal(transport.ETag, db.TodoTaskMetadata.Single().GoogleETag);
            }
            Assert.Equal(1, transport.Writes);
            TodoTaskItem completed = await createService(factory, transport).CompleteTaskAsync(OperationPermissionRegressionTests.createSession(), TodoTaskListId.CreateRequired("tasks"), TodoTaskId.CreateRequired("item"), CancellationToken.None);
            Assert.Equal(form.DueAtUtc, completed.DueAt);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterDeadlineEditSurvivesCompletionOrEarlierStorageFailureAsync(bool shouldFailEarlierSave)
    {
        StorageFailure failure = new StorageFailure();
        using (TestDbContextFactory raw = new TestDbContextFactory(failure))
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.TODOS_UPDATE, raw))
        {
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            TodoTaskFormModel form = new TodoTaskFormModel();
            form.TaskId = "item";
            form.TaskListId = "tasks";
            form.Title = "Updated";
            form.ETag = transport.ETag;
            form.DueAtUtc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
            await createService(factory, transport).UpdateTaskAsync(OperationPermissionRegressionTests.createSession(), form, CancellationToken.None);
            if (shouldFailEarlierSave)
            {
                failure.ShouldFail = true;
                form.ETag = transport.ETag;
                form.DueAtUtc = form.DueAtUtc.Value.AddHours(1);
                await Assert.ThrowsAsync<ResourceSynchronizationException>(() => createService(factory, transport).UpdateTaskAsync(OperationPermissionRegressionTests.createSession(), form, CancellationToken.None));
                failure.ShouldFail = false;
            }
            else
            {
                await createService(factory, transport).CompleteTaskAsync(OperationPermissionRegressionTests.createSession(), TodoTaskListId.CreateRequired("tasks"), TodoTaskId.CreateRequired("item"), CancellationToken.None);
            }
            form.ETag = transport.ETag;
            form.DueAtUtc = new DateTime(2026, 10, 20, 19, 30, 0, DateTimeKind.Utc);
            TodoTaskItem updated = await createService(factory, transport).UpdateTaskAsync(OperationPermissionRegressionTests.createSession(), form, CancellationToken.None);
            Assert.Equal(form.DueAtUtc, updated.DueAt);
            await createService(factory, transport).RecoverDeadlineChangesAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Equal(form.DueAtUtc, db.TodoTaskMetadata.Single().DueAt);
                Assert.Empty(db.TodoDeadlineChanges);
            }
            Assert.Equal("2026-10-20T00:00:00Z", transport.DueOrNull);
        }
    }

    [Fact]
    public async Task FailedIntentStorageDoesNotModifyGoogleAsync()
    {
        StorageFailure failure = new StorageFailure();
        using (TestDbContextFactory raw = new TestDbContextFactory(failure))
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.TODOS_UPDATE, raw))
        {
            VersionedGoogleTransport transport = new VersionedGoogleTransport();
            TodoTaskFormModel form = new TodoTaskFormModel();
            form.TaskId = "item";
            form.TaskListId = "tasks";
            form.Title = "Never written";
            form.ETag = transport.ETag;
            failure.ShouldFailIntent = true;
            await Assert.ThrowsAsync<IOException>(() => createService(factory, transport).UpdateTaskAsync(OperationPermissionRegressionTests.createSession(), form, CancellationToken.None));
            Assert.Equal(0, transport.Writes);
        }
    }

    private static GoogleTasksTodoService createService(TestDbContextFactory factory, VersionedGoogleTransport transport)
    {
        return new GoogleTasksTodoService(factory, transport, NullLogger<GoogleTasksTodoService>.Instance, Options.Create(new GoogleApiOptions()), new ChangeVersionService(), new OrganizationPermissionService(factory));
    }
}
