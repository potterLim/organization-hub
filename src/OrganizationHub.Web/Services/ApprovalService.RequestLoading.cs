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
    private async Task<ApprovalRequest?> loadApprovalRequestOrNullAsync(
        OrganizationHubDbContext dbContext,
        ApprovalRequestId approvalRequestId,
        CancellationToken cancellationToken
    )
    {
        int requiredApprovalRequestId = approvalRequestId.GetRequiredValue();
        ApprovalRequest? requestOrNull = await dbContext.ApprovalRequests
            .Include(request => request.Team)
            .Include(request => request.RequesterUser)
            .ThenInclude(requesterUser => requesterUser!.Team)
            .Include(request => request.RequesterUser)
            .Include(request => request.RequesterUser!.Position)
            .Include(request => request.Steps)
            .Include(request => request.AuditLogs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(request => request.Id == requiredApprovalRequestId, cancellationToken);
        return requestOrNull;
    }

    private static async Task<UserAccount?> loadUserAccountWithAffiliationOrNullAsync(
        OrganizationHubDbContext dbContext,
        UserAccountId userAccountId,
        CancellationToken cancellationToken)
    {
        int requiredUserAccountId = userAccountId.GetRequiredValue();
        return await dbContext.UserAccounts
            .AsNoTracking()
            .Include(userAccount => userAccount.Team)
            .Include(userAccount => userAccount.Position)
            .FirstOrDefaultAsync(userAccount => userAccount.Id == requiredUserAccountId, cancellationToken);
    }

    private ApprovalStep? getCurrentStepOrNull(ApprovalRequest request, UserSession userSession)
    {
        if (request.Status != EApprovalRequestStatus.Pending)
        {
            return null;
        }

        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        return request.Steps.FirstOrDefault(step => step.StepNumber == request.CurrentStepNumber && step.ApproverUserId == currentUserId && step.Status == EApprovalStepStatus.Pending);
    }
}
