using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class AuditRecoveryWorkerTests
{
    private sealed class WorkerDatabaseInterceptor : SaveChangesInterceptor
    {
    }

    [Fact]
    public async Task HostedWorkerRestoresAQueuedReceiptWithoutAUserRequestAsync()
    {
        using (TestDbContextFactory factory = new TestDbContextFactory(new WorkerDatabaseInterceptor()))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                UploadAuditLog receipt = new UploadAuditLog { FileId = "automatic-recovery", FileName = "보고서.pdf" };
                db.PendingUploadAudits.Add(new PendingUploadAudit { Payload = JsonSerializer.Serialize(receipt), CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
            using (IHost host = new HostBuilder().ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IDbContextFactory<OrganizationHubDbContext>>(factory);
                services.AddSingleton<IGoogleApiCredentialFactory, VersionedGoogleTransport>();
                services.AddScoped<AuditRecoveryService>();
                services.AddHostedService<AuditRecoveryWorker>();
            }).Build())
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                await host.StartAsync(timeout.Token);
                while (true)
                {
                    using (OrganizationHubDbContext db = factory.CreateDbContext())
                    {
                        if (await db.UploadAuditLogs.AnyAsync(item => item.FileId == "automatic-recovery", timeout.Token))
                        {
                            break;
                        }
                    }
                    await Task.Delay(20, timeout.Token);
                }
                await host.StopAsync(timeout.Token);
                using (OrganizationHubDbContext db = factory.CreateDbContext())
                {
                    Assert.Single(db.UploadAuditLogs.Where(item => item.FileId == "automatic-recovery"));
                }
            }
        }
    }
}
