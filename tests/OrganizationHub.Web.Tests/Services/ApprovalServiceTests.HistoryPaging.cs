using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class ApprovalServiceTests
{
    [Fact]
    public async Task HistoryPagingReachesOldRequestsAndAppliesAccessBeforePagingAsync()
    {
        using (TestDbContextFactory factory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                for (int index = 0; index < 101; index++)
                {
                    ApprovalRequest own = createRegressionHistoryRequest(1, 1, DateTime.UtcNow.AddDays(-index - 200));
                    own.Title = "내 기록 " + index;
                    db.ApprovalRequests.Add(own);
                }

                for (int index = 0; index < 150; index++)
                {
                    db.ApprovalRequests.Add(createRegressionHistoryRequest(6, 2, DateTime.UtcNow.AddMinutes(-index)));
                }

                await db.SaveChangesAsync();
            }

            ApprovalService service = createHistoryPagingService(factory, cache);
            UserSession session = createSession(1, "planning.member", "요청자", EStoredCompatibilityRole.TeamUser, 1);
            List<ApprovalRequestItem> allItems = new List<ApprovalRequestItem>();
            for (int pageNumber = 1; pageNumber <= 6; pageNumber++)
            {
                ApprovalHistoryPage page = await service.GetHistoryPageAsync(session, createHistoryQuery(pageNumber), CancellationToken.None);
                Assert.Equal(101, page.TotalCount);
                Assert.Equal(6, page.TotalPages);
                allItems.AddRange(page.Items);
            }

            Assert.Equal(101, allItems.Count);
            Assert.Equal(101, allItems.Select(item => item.Id).Distinct().Count());
            Assert.Contains(allItems, item => item.Title == "내 기록 100");
            Assert.All(allItems, item => Assert.Equal(1, item.RequesterUserId));
            ApprovalHistoryPage last = await service.GetHistoryPageAsync(session, createHistoryQuery(int.MaxValue), CancellationToken.None);
            Assert.Equal(6, last.PageNumber);
            Assert.Single(last.Items);
        }
    }

    [Fact]
    public async Task HistorySearchSupportsLiteralWildcardsStatusAndExclusiveDateRangeAsync()
    {
        using (TestDbContextFactory factory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            DateTime requestedAt = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                ApprovalRequest target = createRegressionHistoryRequest(1, 1, requestedAt);
                target.Title = "Budget 100%_승인";
                target.Status = EApprovalRequestStatus.Approved;
                ApprovalRequest decoy = createRegressionHistoryRequest(1, 1, requestedAt.AddDays(1));
                decoy.Title = "Budget 100X승인";
                decoy.Status = EApprovalRequestStatus.Approved;
                db.ApprovalRequests.AddRange(target, decoy);
                await db.SaveChangesAsync();
            }

            ApprovalService service = createHistoryPagingService(factory, cache);
            ApprovalHistoryQuery query = new ApprovalHistoryQuery
            {
                PageNumber = 1,
                Keyword = "budget 100%_",
                Filter = EApprovalHistoryFilter.Approved,
                RequestedFromUtcOrNull = requestedAt,
                RequestedToUtcExclusiveOrNull = requestedAt.AddDays(1)
            };
            ApprovalHistoryPage result = await service.GetHistoryPageAsync(createSession(1, "planning.member", "요청자", EStoredCompatibilityRole.TeamUser, 1), query, CancellationToken.None);
            Assert.Equal("Budget 100%_승인", Assert.Single(result.Items).Title);
            Assert.Equal(1, result.TotalCount);
        }
    }

    [Fact]
    public async Task OwnOngoingPageRetainsPublishingAndExcludesOtherPendingRequestsAsync()
    {
        using (TestDbContextFactory factory = createApprovalDbContextFactory(EFinalApproverSeedMode.SingleFinalApprover))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                ApprovalRequest own = createRegressionHistoryRequest(1, 1, DateTime.UtcNow.AddYears(-1));
                own.Status = EApprovalRequestStatus.Pending;
                ApprovalRequest publishing = createRegressionHistoryRequest(1, 1, DateTime.UtcNow);
                publishing.Status = EApprovalRequestStatus.Publishing;
                ApprovalRequest other = createRegressionHistoryRequest(6, 2, DateTime.UtcNow);
                other.Status = EApprovalRequestStatus.Pending;
                db.ApprovalRequests.AddRange(own, publishing, other, createRegressionHistoryRequest(1, 1, DateTime.UtcNow));
                await db.SaveChangesAsync();
            }

            ApprovalHistoryQuery query = new ApprovalHistoryQuery { PageNumber = 1, Keyword = string.Empty, Filter = EApprovalHistoryFilter.MyOngoing };
            ApprovalHistoryPage result = await createHistoryPagingService(factory, cache).GetHistoryPageAsync(createSession(1, "planning.member", "요청자", EStoredCompatibilityRole.TeamUser, 1), query, CancellationToken.None);
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.MyOngoingCount);
            Assert.All(result.Items, item => Assert.Equal(1, item.RequesterUserId));
            Assert.Contains(result.Items, item => item.Status == EApprovalRequestStatus.Publishing);
        }
    }

    private static ApprovalHistoryQuery createHistoryQuery(int pageNumber)
    {
        return new ApprovalHistoryQuery { PageNumber = pageNumber, Keyword = string.Empty, Filter = EApprovalHistoryFilter.AllRelated };
    }

    private static ApprovalService createHistoryPagingService(TestDbContextFactory factory, MemoryCache cache)
    {
        FakeDriveService drive = createApprovalDriveService();
        OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
        return new ApprovalService(factory, new PermissionService(factory, drive, cache, permissions), drive, permissions);
    }
}
