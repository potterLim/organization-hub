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

public sealed partial class PermissionService
{
    private async Task<bool> canReadApprovalFileAsync(UserSession userSession, DriveItemId fileId, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string requiredFileId = fileId.GetRequiredValue();
            List<ApprovalRequest> approvalRequests = await dbContext.ApprovalRequests.Include(request => request.Steps).Where(request => request.FileId == requiredFileId || request.ApprovedFileId == requiredFileId).ToListAsync(cancellationToken);
            foreach (ApprovalRequest approvalRequest in approvalRequests)
            {
                if (await canReadApprovalRequestFileAsync(userSession, approvalRequest, cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> canReadApprovalRequestFileAsync(
        UserSession userSession,
        ApprovalRequest approvalRequest,
        CancellationToken cancellationToken)
    {
        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        if (approvalRequest.RequesterUserId == currentUserId)
        {
            return true;
        }

        if (canReadApprovalFileAsApprover(userSession, approvalRequest))
        {
            return true;
        }

        OrganizationTeamId? targetTeamIdOrNull = OrganizationTeamId.CreateExistingOrNull(approvalRequest.TeamId);
        return await mOrganizationPermissionService.HasPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.APPROVAL_HISTORY_READ),
            targetTeamIdOrNull,
            cancellationToken);
    }

    private bool canReadApprovalFileAsApprover(UserSession userSession, ApprovalRequest approvalRequest)
    {
        int currentUserId = userSession.UserAccountId.GetRequiredValue();
        foreach (ApprovalStep step in approvalRequest.Steps)
        {
            if (step.ApproverUserId != currentUserId)
            {
                continue;
            }

            if (approvalRequest.Status != EApprovalRequestStatus.Pending)
            {
                return step.Status != EApprovalStepStatus.Pending;
            }

            if (step.StepNumber <= approvalRequest.CurrentStepNumber)
            {
                return true;
            }
        }

        return false;
    }
}
