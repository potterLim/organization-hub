using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalPublicationService
{
    private const int PUBLICATION_PAGE_SIZE = 20;

    public async Task<ApprovalPublicationPage> GetPendingPublicationsAsync(UserSession userSession, int pageNumber, CancellationToken cancellationToken)
    {
        if (await canManagePublicationsAsync(userSession, cancellationToken) == false)
        {
            throw new UnauthorizedAccessException("승인본 저장 작업을 관리할 권한이 없습니다.");
        }

        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<Data.Entities.ApprovalPublication> pending = db.ApprovalPublications.AsNoTracking().Where(item => item.IsCompleted == false);
            int totalCount = await pending.CountAsync(cancellationToken);
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PUBLICATION_PAGE_SIZE));
            int selectedPage = Math.Clamp(pageNumber, 1, totalPages);
            DateTime now = DateTime.UtcNow;
            ApprovalPublicationItem[] items = await pending.OrderBy(item => item.NextAttemptAt).ThenBy(item => item.ApprovalRequestId)
                .Skip((selectedPage - 1) * PUBLICATION_PAGE_SIZE).Take(PUBLICATION_PAGE_SIZE)
                .Select(item => new ApprovalPublicationItem
                {
                    RequestId = item.ApprovalRequestId,
                    Title = item.ApprovalRequest.Title,
                    AttemptCount = item.AttemptCount,
                    LastAttemptAt = item.LastAttemptAt,
                    NextAttemptAt = item.NextAttemptAt,
                    IsProcessing = item.LeaseExpiresAt != null && item.LeaseExpiresAt > now,
                    LastError = ApprovalPublicationMessages.GetUserMessage(item.LastError)
                }).ToArrayAsync(cancellationToken);
            return new ApprovalPublicationPage { Items = items, TotalCount = totalCount, PageNumber = selectedPage, TotalPages = totalPages };
        }
    }

    public async Task<ServiceResult> RetryPublicationAsAdministratorAsync(UserSession userSession, ApprovalRequestId requestId, CancellationToken cancellationToken)
    {
        if (await canManagePublicationsAsync(userSession, cancellationToken) == false)
        {
            return ServiceResult.Fail("승인본 저장 작업을 관리할 권한이 없습니다.");
        }

        // The same lease and persisted target are used by both manual and automatic retries.
        return await PublishApprovedFileAsync(requestId, cancellationToken);
    }

    private Task<bool> canManagePublicationsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        return mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(PermissionKeys.SYSTEM_SETTINGS_MANAGE), null, cancellationToken);
    }
}
