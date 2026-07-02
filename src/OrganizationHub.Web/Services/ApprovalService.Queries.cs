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
                    request.Status == EApprovalRequestStatus.Pending &&
                    request.Steps.Any(step =>
                        step.StepNumber == request.CurrentStepNumber &&
                        step.ApproverUserId == currentUserId &&
                        step.Status == EApprovalStepStatus.Pending))
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
            IQueryable<ApprovalRequest> query = createApprovalRequestDisplayQuery(dbContext)
                .Where(request => request.Status != EApprovalRequestStatus.Pending);

            List<ApprovalRequest> requests = await query
                .OrderByDescending(request => request.RequestedAt)
                .Take(RELATED_HISTORY_CANDIDATE_COUNT)
                .ToListAsync(cancellationToken);
            List<ApprovalRequest> readableRequests = new List<ApprovalRequest>();
            foreach (ApprovalRequest request in requests)
            {
                if (await canReadApprovalHistoryAsync(userSession, request, cancellationToken))
                {
                    readableRequests.Add(request);
                }
            }

            Dictionary<int, UserAccount> actorUsersById = await loadApprovalActorUsersByIdAsync(dbContext, readableRequests, cancellationToken);
            return createApprovalRequestItems(readableRequests, actorUsersById);
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

    private async Task<bool> canReadApprovalHistoryAsync(
        UserSession userSession,
        ApprovalRequest request,
        CancellationToken cancellationToken)
    {
        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        if (request.RequesterUserId == currentUserId)
        {
            return true;
        }

        bool isActionedApprover = request.Steps.Any(step =>
            step.ApproverUserId == currentUserId &&
            step.Status != EApprovalStepStatus.Pending);
        if (isActionedApprover)
        {
            return true;
        }

        OrganizationTeamId? targetTeamIdOrNull = OrganizationTeamId.CreateExistingOrNull(request.TeamId);
        return await mOrganizationPermissionService.HasPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.APPROVAL_HISTORY_READ),
            targetTeamIdOrNull,
            cancellationToken);
    }
}
