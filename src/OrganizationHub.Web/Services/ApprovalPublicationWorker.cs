using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed class ApprovalPublicationWorker : BackgroundService
{
    private const int PUBLICATION_BATCH_SIZE = 10;
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromSeconds(30);
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IServiceScopeFactory mScopeFactory;
    private readonly ILogger<ApprovalPublicationWorker> mLogger;

    public ApprovalPublicationWorker(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IServiceScopeFactory scopeFactory, ILogger<ApprovalPublicationWorker> logger)
    {
        mDbContextFactory = dbContextFactory;
        mScopeFactory = scopeFactory;
        mLogger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested == false)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(stoppingToken))
                {
                    List<int> requestIds = await dbContext.ApprovalPublications.AsNoTracking()
                        .Where(publication => publication.IsCompleted == false && publication.NextAttemptAt <= now &&
                            (publication.LeaseExpiresAt == null || publication.LeaseExpiresAt <= now))
                        .OrderBy(publication => publication.NextAttemptAt).Take(PUBLICATION_BATCH_SIZE)
                        .Select(publication => publication.ApprovalRequestId).ToListAsync(stoppingToken);
                    foreach (int requestId in requestIds)
                    {
                        using (IServiceScope scope = mScopeFactory.CreateScope())
                        {
                            IApprovalPublicationService publicationService = scope.ServiceProvider.GetRequiredService<IApprovalPublicationService>();
                            await publicationService.PublishApprovedFileAsync(ApprovalRequestId.CreateExisting(requestId), stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                mLogger.LogWarning(exception, "Approval publication recovery will be retried.");
            }

            try
            {
                await Task.Delay(POLL_INTERVAL, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
