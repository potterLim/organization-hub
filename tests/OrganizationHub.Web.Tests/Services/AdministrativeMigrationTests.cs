using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class AdministrativeMigrationTests
{
    [Fact]
    public async Task MigrationPreservesAccountsAndTracksAdministrativeChangesOnlyAsync()
    {
        using (SqliteConnection connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            DbContextOptionsBuilder<OrganizationHubDbContext> options = new DbContextOptionsBuilder<OrganizationHubDbContext>();
            options.UseSqlite(connection);
            using (OrganizationHubDbContext db = new OrganizationHubDbContext(options.Options))
            {
                IMigrator migrator = db.GetService<IMigrator>();
                string previousMigration = db.Database.GetMigrations().Single(migration => migration.EndsWith("_ProtectSettingsConcurrency"));
                await migrator.MigrateAsync(previousMigration);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO Teams(Id,TeamKey,DisplayName,IsActive,SortOrder) VALUES(1,'Planning','기획팀',1,10); " +
                        "INSERT INTO Positions(Id,TeamId,Name,Description,SortOrder,IsActive,IsSystemAdministrator) VALUES(1,1,'담당자','',10,1,0); " +
                        "INSERT INTO UserAccounts(Id,Username,DisplayName,PasswordHash,Role,TeamId,PositionId,IsActive,CreatedAt) VALUES(1,'member','담당자','preserved-hash','TeamUser',1,1,1,'2026-10-19 00:00:00');";
                    await command.ExecuteNonQueryAsync();
                }

                await migrator.MigrateAsync();
                UserAccount user = await db.UserAccounts.SingleAsync();
                OrganizationPosition position = await db.Positions.SingleAsync();
                Assert.Equal("preserved-hash", user.PasswordHash);
                Assert.Equal(0, user.Revision);
                Assert.Equal(0, position.Revision);
                user.LastLoginAt = System.DateTime.UtcNow;
                await db.SaveChangesAsync();
                Assert.Equal(0, user.Revision);
                user.DisplayName = "변경된 담당자";
                PositionPermissionGrant grant = new PositionPermissionGrant();
                grant.PositionId = position.Id;
                grant.PermissionKey = PermissionKeys.CALENDAR_READ;
                grant.IsAllowed = true;
                grant.ScopeType = EPermissionScopeType.OwnTeam;
                db.PositionPermissionGrants.Add(grant);
                await db.SaveChangesAsync();
                Assert.Equal(1, user.Revision);
                Assert.Equal(1, position.Revision);
                Assert.False(db.Database.HasPendingModelChanges());
            }
        }
    }
}
