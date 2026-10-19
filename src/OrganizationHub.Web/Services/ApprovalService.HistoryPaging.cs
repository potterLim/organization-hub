using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private const int HISTORY_PAGE_SIZE = 20;
    private const int HISTORY_KEYWORD_MAXIMUM_LENGTH = 120;

    public async Task<ApprovalHistoryPage> GetHistoryPageAsync(UserSession userSession, ApprovalHistoryQuery query, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int userId = userSession.UserAccountId.GetRequiredValue();
            IQueryable<ApprovalRequest> ownOngoing = db.ApprovalRequests.Where(request => request.RequesterUserId == userId);
            ownOngoing = ownOngoing.Where(request => request.Status == EApprovalRequestStatus.Pending || request.Status == EApprovalRequestStatus.Publishing);
            int ongoingCount = await ownOngoing.CountAsync(cancellationToken);
            IQueryable<ApprovalRequest> requests = createApprovalRequestDisplayQuery(db).AsNoTracking();
            IReadOnlyList<int?> readableTeamIds = await getReadableHistoryTeamIdsAsync(userSession, requests, cancellationToken);
            IQueryable<int> actedRequestIds = db.ApprovalSteps.Where(step => step.ApproverUserId == userId && step.Status != EApprovalStepStatus.Pending).Select(step => step.ApprovalRequestId);
            IQueryable<int> relatedRequestIds = db.ApprovalRequests.Where(request => request.Status != EApprovalRequestStatus.Pending)
                .Where(request => readableTeamIds.Contains(request.TeamId) || actedRequestIds.Contains(request.Id)).Select(request => request.Id);
            requests = requests.Where(request => request.RequesterUserId == userId || relatedRequestIds.Contains(request.Id));
            requests = applyHistoryFilter(requests, query, userId);
            int totalCount = await requests.CountAsync(cancellationToken);
            int totalPages = Math.Max(1, (int)((totalCount + (long)HISTORY_PAGE_SIZE - 1) / HISTORY_PAGE_SIZE));
            int pageNumber = Math.Clamp(query.PageNumber, 1, totalPages);
            IQueryable<int> ownOngoingIds = ownOngoing.Select(request => request.Id);
            List<ApprovalRequest> page = await requests
                .OrderByDescending(request => ownOngoingIds.Contains(request.Id))
                .ThenByDescending(request => request.RequestedAt)
                .ThenByDescending(request => request.Id)
                .Skip((pageNumber - 1) * HISTORY_PAGE_SIZE)
                .Take(HISTORY_PAGE_SIZE)
                .ToListAsync(cancellationToken);
            Dictionary<int, UserAccount> actors = await loadApprovalActorUsersByIdAsync(db, page, cancellationToken);
            return new ApprovalHistoryPage
            {
                Items = createApprovalRequestItems(page, actors),
                PageNumber = pageNumber,
                TotalPages = totalPages,
                TotalCount = totalCount,
                MyOngoingCount = ongoingCount
            };
        }
    }

    public async Task<OwnApprovalProgress> GetOwnProgressAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int userId = userSession.UserAccountId.GetRequiredValue();
            IQueryable<ApprovalRequest> ownRequests = db.ApprovalRequests.Where(request => request.RequesterUserId == userId);
            List<OwnApprovalProgress> summaries = await ownRequests.GroupBy(request => request.RequesterUserId)
                .Select(group => new OwnApprovalProgress
                {
                    PendingCount = group.Count(request => request.Status == EApprovalRequestStatus.Pending),
                    PublishingCount = group.Count(request => request.Status == EApprovalRequestStatus.Publishing)
                }).ToListAsync(cancellationToken);
            if (summaries.Count == 0)
            {
                return new OwnApprovalProgress { PendingCount = 0, PublishingCount = 0 };
            }

            return summaries[0];
        }
    }

    private static IQueryable<ApprovalRequest> applyHistoryFilter(IQueryable<ApprovalRequest> requests, ApprovalHistoryQuery query, int userId)
    {
        switch (query.Filter)
        {
            case EApprovalHistoryFilter.MyOngoing:
                requests = requests.Where(request => request.RequesterUserId == userId);
                requests = requests.Where(request => request.Status == EApprovalRequestStatus.Pending || request.Status == EApprovalRequestStatus.Publishing);
                break;
            case EApprovalHistoryFilter.Approved:
                requests = requests.Where(request => request.Status == EApprovalRequestStatus.Approved);
                break;
            case EApprovalHistoryFilter.Rejected:
                requests = requests.Where(request => request.Status == EApprovalRequestStatus.Rejected);
                break;
            case EApprovalHistoryFilter.AllRelated:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(query));
        }

        string keyword = query.Keyword.Trim();
        if (keyword.Length > HISTORY_KEYWORD_MAXIMUM_LENGTH)
        {
            keyword = keyword.Substring(0, HISTORY_KEYWORD_MAXIMUM_LENGTH);
        }

        if (keyword.Length > 0)
        {
            string escaped = keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            string pattern = "%" + escaped + "%";
            requests = requests.Where(request => EF.Functions.Like(request.Title, pattern, "\\") || EF.Functions.Like(request.FileName, pattern, "\\"));
        }

        if (query.RequestedFromUtcOrNull.HasValue)
        {
            requests = requests.Where(request => request.RequestedAt >= query.RequestedFromUtcOrNull.Value);
        }

        if (query.RequestedToUtcExclusiveOrNull.HasValue)
        {
            requests = requests.Where(request => request.RequestedAt < query.RequestedToUtcExclusiveOrNull.Value);
        }

        return requests;
    }
}
