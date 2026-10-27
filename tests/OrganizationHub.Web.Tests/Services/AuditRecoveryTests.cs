using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class AuditRecoveryTests
{
    [Fact]
    public async Task FailedOlderDeletionRecordsDoNotStarveNewerRecordsAsync()
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        using (VersionedGoogleTransport transport = new VersionedGoogleTransport())
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                for (int index = 0; index < 21; index++)
                {
                    CalendarEventDeletionAuditLog deletion = new CalendarEventDeletionAuditLog();
                    deletion.CalendarId = "calendar";
                    deletion.EventId = "event-" + index;
                    deletion.DeletedAt = DateTime.UtcNow.AddMinutes(-10);
                    deletion.Status = ECalendarEventDeletionAuditStatus.Pending;
                    db.CalendarEventDeletionAuditLogs.Add(deletion);
                }
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_old_deletions BEFORE UPDATE ON CalendarEventDeletionAuditLogs WHEN OLD.Id <= 20 AND NEW.Status <> OLD.Status BEGIN SELECT RAISE(FAIL,'old deletion receipt unavailable'); END;");
            }
            AuditRecoveryService recovery = new AuditRecoveryService(factory, transport, NullLogger<AuditRecoveryService>.Instance);
            await recovery.RecoverAsync(CancellationToken.None);
            await recovery.RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Equal(ECalendarEventDeletionAuditStatus.Failed, db.CalendarEventDeletionAuditLogs.Single(item => item.Id == 21).Status);
                Assert.Equal(20, db.CalendarEventDeletionAuditLogs.Count(item => item.Status == ECalendarEventDeletionAuditStatus.Pending));
            }
        }
    }

    [Fact]
    public async Task FailedOlderReceiptsDoNotStarveNewReceiptsAsync()
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        using (VersionedGoogleTransport transport = new VersionedGoogleTransport())
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                for (int index = 0; index < 21; index++)
                {
                    UploadAuditLog receipt = new UploadAuditLog();
                    receipt.FileId = index < 20 ? "blocked-" + index : "new-valid";
                    PendingUploadAudit pending = new PendingUploadAudit();
                    pending.Payload = JsonSerializer.Serialize(receipt);
                    pending.CreatedAt = DateTime.UtcNow;
                    db.PendingUploadAudits.Add(pending);
                }
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_old BEFORE INSERT ON UploadAuditLogs WHEN NEW.FileId LIKE 'blocked-%' BEGIN SELECT RAISE(FAIL,'old receipt unavailable'); END;");
            }
            AuditRecoveryService recovery = new AuditRecoveryService(factory, transport, NullLogger<AuditRecoveryService>.Instance);
            await recovery.RecoverAsync(CancellationToken.None);
            await recovery.RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Single(db.UploadAuditLogs.Where(item => item.FileId == "new-valid"));
                Assert.Equal(20, db.PendingUploadAudits.Count());
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_old;");
            }
            await recovery.RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.PendingUploadAudits);
                Assert.Equal(21, db.UploadAuditLogs.Count());
            }
        }
    }

    [Theory]
    [InlineData(true, ECalendarEventDeletionAuditStatus.Succeeded)]
    [InlineData(false, ECalendarEventDeletionAuditStatus.Failed)]
    public async Task StaleDeletionIsReconciledWithoutRepeatingDeleteAsync(bool isDeleted, ECalendarEventDeletionAuditStatus expected)
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        using (VersionedGoogleTransport transport = new VersionedGoogleTransport())
        {
            if (isDeleted)
            {
                BaseClientService.Initializer initializer = new BaseClientService.Initializer();
                initializer.HttpClientInitializer = transport;
                using (CalendarService calendar = new CalendarService(initializer))
                {
                    await calendar.Events.Delete("calendar", "event").ExecuteAsync();
                }
            }
            int previousDeletes = transport.Deletes;
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                db.CalendarEventDeletionAuditLogs.Add(new CalendarEventDeletionAuditLog
                {
                    CalendarId = "calendar",
                    EventId = "event",
                    DeletedAt = DateTime.UtcNow.AddMinutes(-10),
                    Status = ECalendarEventDeletionAuditStatus.Pending
                });
                db.CalendarEventDeletionAuditLogs.Add(new CalendarEventDeletionAuditLog
                {
                    CalendarId = "calendar",
                    EventId = "recent",
                    DeletedAt = DateTime.UtcNow,
                    Status = ECalendarEventDeletionAuditStatus.Pending
                });
                await db.SaveChangesAsync();
            }
            AuditRecoveryService recovery = new AuditRecoveryService(factory, transport, NullLogger<AuditRecoveryService>.Instance);
            await recovery.RecoverAsync(CancellationToken.None);
            await recovery.RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Equal(expected, db.CalendarEventDeletionAuditLogs.Single(item => item.EventId == "event").Status);
                Assert.Equal(ECalendarEventDeletionAuditStatus.Pending, db.CalendarEventDeletionAuditLogs.Single(item => item.EventId == "recent").Status);
                Assert.Equal(previousDeletes, transport.Deletes);
            }
        }
    }
}
