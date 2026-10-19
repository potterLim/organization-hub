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

public sealed class PublicationDiagnosticsMigrationTests
{
    [Fact]
    public async Task MigrationPreservesPendingPublicationIdentityAndSnapshotAsync()
    {
        using (SqliteConnection connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            DbContextOptionsBuilder<OrganizationHubDbContext> options = new DbContextOptionsBuilder<OrganizationHubDbContext>();
            options.UseSqlite(connection);
            using (OrganizationHubDbContext db = new OrganizationHubDbContext(options.Options))
            {
                IMigrator migrator = db.GetService<IMigrator>();
                string previousMigration = db.Database.GetMigrations().Single(migration => migration.EndsWith("_RevokeSessionsAfterPasswordChanges", StringComparison.Ordinal));
                await migrator.MigrateAsync(previousMigration);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
                        INSERT INTO UserAccounts (Id, Username, DisplayName, PasswordHash, Role, IsActive, CreatedAt)
                        VALUES (1, 'requester', 'Requester', 'unused', 'TeamUser', 1, '2026-01-01 00:00:00');
                        INSERT INTO ApprovalRequests (Id, Title, FileId, FileName, MimeType, SourceFolderId, SourceFolderKind,
                            TeamName, RequesterUserId, RequesterDisplayName, RequestedAt, Status, StepCount, CurrentStepNumber,
                            ApprovedFileId, ApprovedFolderId, ApprovedFolderDisplayName, RequesterTeamName, RequesterPositionName, DocumentHash)
                        VALUES (1, 'Pending publication', 'source-file', 'snapshot.pdf', 'application/pdf', 'source', 'TeamMaterials',
                            '', 1, 'Requester', '2026-01-01 00:00:00', 'Publishing', 1, 1, '', 'approved', '', '', '', 'preserved-hash');
                        INSERT INTO ApprovalDocuments (ApprovalRequestId, Content, FileName, ContentType)
                        VALUES (1, X'010203', 'snapshot.pdf', 'application/pdf');
                        INSERT INTO ApprovalPublications (ApprovalRequestId, TargetFileId, FileName, OperationKey,
                            IsReplacement, IsCompleted, LeaseToken, LeaseExpiresAt, NextAttemptAt)
                        VALUES (1, 'reserved-file', 'snapshot.pdf', 'original-operation', 0, 0, 'existing-lease',
                            '2030-01-01 00:00:00', '2030-01-01 00:00:00');
                        """;
                    await command.ExecuteNonQueryAsync();
                }

                await migrator.MigrateAsync();
                ApprovalPublication publication = await db.ApprovalPublications.SingleAsync();
                Assert.Equal("reserved-file", publication.TargetFileId);
                Assert.Equal("original-operation", publication.OperationKey);
                Assert.Equal("existing-lease", publication.LeaseToken);
                Assert.False(publication.IsCompleted);
                Assert.Equal(0, publication.AttemptCount);
                Assert.Null(publication.LastAttemptAt);
                Assert.Empty(publication.LastError);
                Assert.Equal(new byte[] { 1, 2, 3 }, (await db.ApprovalDocuments.SingleAsync()).Content);
                ApprovalRequest request = await db.ApprovalRequests.SingleAsync();
                Assert.Equal(EApprovalRequestStatus.Publishing, request.Status);
                Assert.Equal("preserved-hash", request.DocumentHash);
                Assert.False(db.Database.HasPendingModelChanges());
            }
        }
    }
}
