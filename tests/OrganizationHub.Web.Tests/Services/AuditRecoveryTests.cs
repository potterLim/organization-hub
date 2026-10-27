using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
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
