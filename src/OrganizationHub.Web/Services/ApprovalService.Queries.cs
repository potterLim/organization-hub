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
    public async Task<IReadOnlyList<ApprovalRequestItem>> GetPendingApprovalsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int currentUserId = userSession.UserAccountId.GetRequiredValue();
            List<ApprovalRequest> requests = await createApprovalRequestDisplayQuery(dbContext)
                .Where(request =>
                    (request.Status == EApprovalRequestStatus.Pending || request.Status == EApprovalRequestStatus.Publishing) &&
                    request.Steps.Any(step =>
                        step.StepNumber == request.CurrentStepNumber &&
                        step.ApproverUserId == currentUserId &&
                        (step.Status == EApprovalStepStatus.Pending || request.Status == EApprovalRequestStatus.Publishing)))
                .OrderBy(request => request.RequestedAt)
                .ToListAsync(cancellationToken);
            Dictionary<int, UserAccount> actorUsersById = await loadApprovalActorUsersByIdAsync(dbContext, requests, cancellationToken);
            return createApprovalRequestItems(requests, actorUsersById);
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestItem>> GetRelatedHistoryAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int currentUserId = userSession.UserAccountId.GetRequiredValue();
            List<ApprovalRequest> ongoingRequests = await createApprovalRequestDisplayQuery(dbContext)
                .Where(request => request.RequesterUserId == currentUserId)
                .Where(request => request.Status == EApprovalRequestStatus.Pending || request.Status == EApprovalRequestStatus.Publishing)
                .OrderBy(request => request.RequestedAt)
                .ThenBy(request => request.Id)
                .ToListAsync(cancellationToken);
            IQueryable<ApprovalRequest> query = createApprovalRequestDisplayQuery(dbContext)
                .Where(request => request.Status != EApprovalRequestStatus.Pending)
                .Where(request => request.Status != EApprovalRequestStatus.Publishing || request.RequesterUserId != currentUserId);
            IReadOnlyList<int?> readableTeamIds = await getReadableHistoryTeamIdsAsync(userSession, query, cancellationToken);
            List<ApprovalRequest> readableRequests = await query
                .Where(request =>
                    request.RequesterUserId == currentUserId ||
                    request.Steps.Any(step => step.ApproverUserId == currentUserId && step.Status != EApprovalStepStatus.Pending) ||
                    readableTeamIds.Contains(request.TeamId))
                .OrderByDescending(request => request.RequestedAt)
                .ThenByDescending(request => request.Id)
                .Take(RELATED_HISTORY_MAXIMUM_COUNT)
                .ToListAsync(cancellationToken);

            HashSet<int> ongoingRequestIds = ongoingRequests.Select(request => request.Id).ToHashSet();
            ongoingRequests.AddRange(readableRequests.Where(request => ongoingRequestIds.Contains(request.Id) == false));
            Dictionary<int, UserAccount> actorUsersById = await loadApprovalActorUsersByIdAsync(dbContext, ongoingRequests, cancellationToken);
            return createApprovalRequestItems(ongoingRequests, actorUsersById);
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestItem>> GetApprovedRequestsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<ApprovalRequest> query = createApprovalRequestDisplayQuery(dbContext)
                .Where(request => request.Status == EApprovalRequestStatus.Approved);
            List<ApprovalRequest> requests = await query
                .OrderByDescending(request => request.CompletedAt)
                .ThenByDescending(request => request.RequestedAt)
                .ToListAsync(cancellationToken);
            List<ApprovalRequest> readableRequests = await filterApprovedRequestsByDestinationAsync(userSession, requests, cancellationToken);
            Dictionary<int, UserAccount> actorUsersById = await loadApprovalActorUsersByIdAsync(dbContext, readableRequests, cancellationToken);
            return createApprovalRequestItems(readableRequests, actorUsersById);
        }
    }

    private static IQueryable<ApprovalRequest> createApprovalRequestDisplayQuery(OrganizationHubDbContext dbContext)
    {
        return dbContext.ApprovalRequests
            .Include(request => request.RequesterUser)
            .ThenInclude(requesterUser => requesterUser!.Team)
            .Include(request => request.RequesterUser)
            .ThenInclude(requesterUser => requesterUser!.Position)
            .Include(request => request.Steps)
            .ThenInclude(step => step.ApproverUser)
            .ThenInclude(approverUser => approverUser!.Team)
            .Include(request => request.Steps)
            .ThenInclude(step => step.ApproverUser)
            .ThenInclude(approverUser => approverUser!.Position)
            .Include(request => request.AuditLogs)
            .AsSplitQuery();
    }

    private static async Task<Dictionary<int, UserAccount>> loadApprovalActorUsersByIdAsync(
        OrganizationHubDbContext dbContext,
        IReadOnlyList<ApprovalRequest> requests,
        CancellationToken cancellationToken)
    {
        List<int> actorUserIds = requests
            .SelectMany(request => request.AuditLogs)
            .Select(auditLog => auditLog.ActorUserId)
            .Distinct()
            .ToList();
        if (actorUserIds.Count == 0)
        {
            return new Dictionary<int, UserAccount>();
        }

        List<UserAccount> actorUsers = await dbContext.UserAccounts
            .AsNoTracking()
            .Include(user => user.Team)
            .Include(user => user.Position)
            .Where(user => actorUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return actorUsers.ToDictionary(user => user.Id);
    }

    private async Task<IReadOnlyList<int?>> getReadableHistoryTeamIdsAsync(
        UserSession userSession,
        IQueryable<ApprovalRequest> query,
        CancellationToken cancellationToken)
    {
        List<int?> teamIds = await query.Select(request => request.TeamId).Distinct().ToListAsync(cancellationToken);
        List<int?> readableTeamIds = new List<int?>();
        foreach (int? teamIdOrNull in teamIds)
        {
            OrganizationTeamId? targetTeamIdOrNull = OrganizationTeamId.CreateExistingOrNull(teamIdOrNull);
            bool canReadHistory = await mOrganizationPermissionService.HasPermissionAsync(
                userSession,
                new PermissionKey(PermissionKeys.APPROVAL_HISTORY_READ),
                targetTeamIdOrNull,
                cancellationToken);
            if (canReadHistory)
            {
                readableTeamIds.Add(teamIdOrNull);
            }
        }

        return readableTeamIds;
    }
}
