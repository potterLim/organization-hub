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
            List<ApprovalRequest> requests = await dbContext.ApprovalRequests
                .Include(request => request.Steps)
                .Include(request => request.AuditLogs)
                .AsSplitQuery()
                .Where(request =>
                    request.Status == EApprovalRequestStatus.Pending &&
                    request.Steps.Any(step =>
                        step.StepNumber == request.CurrentStepNumber &&
                        step.ApproverUserId == userSession.UserId &&
                        step.Status == EApprovalStepStatus.Pending))
                .OrderBy(request => request.RequestedAt)
                .ToListAsync(cancellationToken);
            return createApprovalRequestItems(requests);
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestItem>> GetRelatedHistoryAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<ApprovalRequest> query = dbContext.ApprovalRequests
                .Include(request => request.Steps)
                .Include(request => request.AuditLogs)
                .AsSplitQuery()
                .Where(request => request.Status != EApprovalRequestStatus.Pending);
            if (userSession.Role != EUserRole.Admin)
            {
                int userId = userSession.UserId;
                query = query.Where(request =>
                    request.RequesterUserId == userId ||
                    request.Steps.Any(step =>
                        step.ApproverUserId == userId &&
                        step.Status != EApprovalStepStatus.Pending));
            }

            List<ApprovalRequest> requests = await query
                .OrderByDescending(request => request.RequestedAt)
                .Take(100)
                .ToListAsync(cancellationToken);
            return createApprovalRequestItems(requests);
        }
    }

    public async Task<IReadOnlyList<ApprovalRequestItem>> GetApprovedRequestsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<ApprovalRequest> query = dbContext.ApprovalRequests
                .Include(request => request.Steps)
                .Include(request => request.AuditLogs)
                .AsSplitQuery()
                .Where(request => request.Status == EApprovalRequestStatus.Approved);
            if (userSession.IsManagementRole() == false)
            {
                if (userSession.HasTeam == false)
                {
                    return new List<ApprovalRequestItem>();
                }

                int teamId = userSession.GetRequiredTeamIdValue();
                query = query.Where(request => request.TeamId == teamId);
            }

            List<ApprovalRequest> requests = await query
                .OrderByDescending(request => request.CompletedAt)
                .ThenByDescending(request => request.RequestedAt)
                .ToListAsync(cancellationToken);
            List<ApprovalRequest> readableRequests = await filterApprovedRequestsByDestinationAsync(userSession, requests, cancellationToken);
            return createApprovalRequestItems(readableRequests);
        }
    }
}
