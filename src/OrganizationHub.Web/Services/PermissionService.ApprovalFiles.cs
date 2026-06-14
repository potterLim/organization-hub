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
                if (canReadApprovalRequestFile(userSession, approvalRequest))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool canReadApprovalRequestFile(UserSession userSession, ApprovalRequest approvalRequest)
    {
        if (userSession.Role == EUserRole.Admin)
        {
            return true;
        }

        if (approvalRequest.RequesterUserId == userSession.UserId)
        {
            return true;
        }

        foreach (ApprovalStep step in approvalRequest.Steps)
        {
            if (step.ApproverUserId != userSession.UserId)
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
