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

public sealed class SettingsMigrationTests
{
    [Fact]
    public async Task MigrationPreservesExistingSettingsAndEnablesRevisionTrackingAsync()
    {
        using (SqliteConnection connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            DbContextOptionsBuilder<OrganizationHubDbContext> options = new DbContextOptionsBuilder<OrganizationHubDbContext>();
            options.UseSqlite(connection);
            using (OrganizationHubDbContext db = new OrganizationHubDbContext(options.Options))
            {
                string[] migrations = db.Database.GetMigrations().ToArray();
                int targetIndex = Array.FindIndex(migrations, migration => migration.EndsWith("_ProtectSettingsConcurrency", StringComparison.Ordinal));
                IMigrator migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(migrations[targetIndex - 1]);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO GlobalSettings (Id,OfficialRootFolderId,ShowTodoAuthor,ShowCalendarAuthor) VALUES(1,'preserved-root',0,1)";
                    await command.ExecuteNonQueryAsync();
                }

                await migrator.MigrateAsync();
                GlobalSetting setting = await db.GlobalSettings.SingleAsync();
                Assert.Equal("preserved-root", setting.OfficialRootFolderId);
                Assert.False(setting.ShouldShowTodoAuthor);
                Assert.True(setting.ShouldShowCalendarAuthor);
                Assert.Equal(0, setting.Revision);
                setting.ShouldShowCalendarAuthor = false;
                await db.SaveChangesAsync();
                Assert.Equal(1, setting.Revision);
                Assert.Equal(1, setting.DisplayOptionsRevision);
                Assert.Equal(0, setting.FileResourcesRevision);
                Assert.False(db.Database.HasPendingModelChanges());
            }
        }
    }
}
