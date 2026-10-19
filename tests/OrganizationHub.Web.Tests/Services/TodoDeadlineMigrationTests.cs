using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class TodoDeadlineMigrationTests
{
    [Fact]
    public async Task MigrationPreservesTaskOwnershipAndDueTimeAndStoresRecoverableChangesAsync()
    {
        using (SqliteConnection connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            DbContextOptionsBuilder<OrganizationHubDbContext> options = new DbContextOptionsBuilder<OrganizationHubDbContext>();
            options.UseSqlite(connection);
            using (OrganizationHubDbContext db = new OrganizationHubDbContext(options.Options))
            {
                IMigrator migrator = db.GetService<IMigrator>();
                string[] migrations = db.Database.GetMigrations().ToArray();
                int targetIndex = Array.FindIndex(migrations, migration => migration.EndsWith("_RecoverTodoDeadlineChanges", StringComparison.Ordinal));
                await migrator.MigrateAsync(migrations[targetIndex - 1]);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO TodoTaskMetadata (Id,TaskListId,TaskId,CreatedByUserId,CreatedByDisplayName,CreatedAt,DueAt) VALUES(1,'list','task',42,'Original owner','2026-10-18 00:00:00','2026-10-20 12:30:00');";
                    await command.ExecuteNonQueryAsync();
                }
                await migrator.MigrateAsync();
                TodoTaskMetadata metadata = await db.TodoTaskMetadata.SingleAsync();
                Assert.Equal(42, metadata.CreatedByUserId);
                Assert.Equal("Original owner", metadata.CreatedByDisplayName);
                Assert.Equal(new DateTime(2026, 10, 20, 12, 30, 0), metadata.DueAt);
                Assert.Empty(metadata.GoogleETag);
                TodoDeadlineChange change = new TodoDeadlineChange();
                change.TaskListId = "list";
                change.TaskId = "task";
                change.Title = "Cleared deadline";
                change.ExpectedETag = "\"original-version\"";
                change.CreatedAt = DateTime.UtcNow;
                db.TodoDeadlineChanges.Add(change);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                TodoDeadlineChange stored = await db.TodoDeadlineChanges.SingleAsync();
                Assert.Equal("\"original-version\"", stored.ExpectedETag);
                Assert.Null(stored.DueAt);
                Assert.False(db.Database.HasPendingModelChanges());
            }
        }
    }
}
