using System;
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

public sealed class ApprovalDocumentMigrationTests
{
    [Fact]
    public async Task ApprovalMigrationPreservesLegacyRequestsWithoutInventingSnapshotsAsync()
    {
        using (SqliteConnection connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            DbContextOptionsBuilder<OrganizationHubDbContext> optionsBuilder = new DbContextOptionsBuilder<OrganizationHubDbContext>();
            optionsBuilder.UseSqlite(connection);
            using (OrganizationHubDbContext dbContext = new OrganizationHubDbContext(optionsBuilder.Options))
            {
                string[] migrations = dbContext.Database.GetMigrations().ToArray();
                IMigrator migrator = dbContext.GetService<IMigrator>();
                int approvalMigrationIndex = Array.FindIndex(migrations, migration => migration.EndsWith("_PreserveApprovalDocumentsAndPublication", StringComparison.Ordinal));
                await migrator.MigrateAsync(migrations[approvalMigrationIndex - 1]);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
                        INSERT INTO UserAccounts (Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt)
                        VALUES (1, 'legacy', 'Legacy requester', 'unused', 'TeamUser', 1, '2026-01-01 00:00:00');
                        INSERT INTO ApprovalRequests (Title, FileId, FileName, MimeType, SourceFolderId, SourceFolderKind,
                            TeamName, RequesterUserId, RequesterDisplayName, RequestedAt, Status, StepCount, CurrentStepNumber,
                            ApprovedFileId, ApprovedFolderId, ApprovedFolderDisplayName, RequesterTeamName, RequesterPositionName)
                        VALUES ('Legacy request', 'legacy-file', 'legacy.pdf', 'application/pdf', 'source', 'TeamMaterials',
                            '', 1, 'Legacy requester', '2026-01-01 00:00:00', 'Pending', 1, 1, '', 'approved', '', '', '');
                        """;
                    await command.ExecuteNonQueryAsync();
                }

                await migrator.MigrateAsync();
                ApprovalRequest legacyRequest = await dbContext.ApprovalRequests.SingleAsync();
                Assert.Equal("Legacy request", legacyRequest.Title);
                Assert.Equal(EApprovalRequestStatus.Pending, legacyRequest.Status);
                Assert.Equal(0, legacyRequest.Revision);
                Assert.Empty(legacyRequest.DocumentHash);
                Assert.Empty(dbContext.ApprovalDocuments);
                Assert.Empty(dbContext.ApprovalPublications);
                Assert.False(dbContext.Database.HasPendingModelChanges());

                ApprovalDocument document = new ApprovalDocument();
                document.ApprovalRequestId = legacyRequest.Id;
                document.Content = new byte[] { 1, 2, 3 };
                document.FileName = "preserved.pdf";
                document.ContentType = "application/pdf";
                dbContext.ApprovalDocuments.Add(document);
                ApprovalPublication publication = new ApprovalPublication();
                publication.ApprovalRequestId = legacyRequest.Id;
                publication.TargetFileId = "reserved-file";
                publication.OperationKey = Guid.NewGuid().ToString("N");
                publication.NextAttemptAt = DateTime.UtcNow;
                dbContext.ApprovalPublications.Add(publication);
                await dbContext.SaveChangesAsync();
                Assert.Single(dbContext.ApprovalDocuments);
                Assert.Single(dbContext.ApprovalPublications);
            }
        }
    }
}
