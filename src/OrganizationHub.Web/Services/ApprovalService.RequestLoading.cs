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
            .Include(request => request.Steps)
            .Include(request => request.AuditLogs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(request => request.Id == requiredApprovalRequestId, cancellationToken);
        return requestOrNull;
    }

    private ApprovalStep? getCurrentStepOrNull(ApprovalRequest request, UserSession userSession)
    {
        if (request.Status != EApprovalRequestStatus.Pending)
        {
            return null;
        }

        return request.Steps.FirstOrDefault(step => step.StepNumber == request.CurrentStepNumber && step.ApproverUserId == userSession.UserId && step.Status == EApprovalStepStatus.Pending);
    }
}
