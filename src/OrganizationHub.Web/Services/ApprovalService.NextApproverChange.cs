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
    private async Task<ServiceResult> changeNextApproverIfRequestedAsync(
        OrganizationHubDbContext dbContext,
        ApprovalRequest request,
        UserSession userSession,
        ApprovalUserId nextApproverUserId,
        CancellationToken cancellationToken
    )
    {
        if (nextApproverUserId.HasValue == false)
        {
            return ServiceResult.Succeed();
        }

        int nextStepNumber = request.CurrentStepNumber + 1;
        ApprovalStep? nextStepOrNull = request.Steps.FirstOrDefault(step => step.StepNumber == nextStepNumber);
        if (nextStepOrNull == null)
        {
            return ServiceResult.Fail("다음 결재 단계를 찾을 수 없습니다.");
        }

        if (nextStepNumber >= request.StepCount)
        {
            if (nextStepOrNull.ApproverUserId == nextApproverUserId.GetRequiredValue())
            {
                return ServiceResult.Succeed();
            }

            return ServiceResult.Fail("최종 결재권자는 변경할 수 없습니다.");
        }

        int requiredNextApproverUserId = nextApproverUserId.GetRequiredValue();
        UserAccount? nextApproverOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Id == requiredNextApproverUserId && user.IsActive, cancellationToken);
        if (nextApproverOrNull == null || canUseAsChangedIntermediateApprover(request, nextApproverOrNull) == false)
        {
            return ServiceResult.Fail("변경할 결재권자를 찾을 수 없습니다.");
        }

        if (nextApproverOrNull.Id == userSession.UserId)
        {
            return ServiceResult.Fail("다음 결재권자를 자기 자신으로 지정할 수 없습니다.");
        }

        foreach (ApprovalStep step in request.Steps)
        {
            if (step.StepNumber == nextStepNumber)
            {
                continue;
            }

            if (step.ApproverUserId == nextApproverOrNull.Id)
            {
                return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
            }
        }

        string previousApproverDisplayName = nextStepOrNull.ApproverDisplayName;
        nextStepOrNull.ApproverUserId = nextApproverOrNull.Id;
        nextStepOrNull.ApproverDisplayName = nextApproverOrNull.DisplayName;
        request.AuditLogs.Add(createAuditLog(createApproverChangedAuditLogRequest(userSession, nextStepNumber, previousApproverDisplayName, nextApproverOrNull.DisplayName)));
        return ServiceResult.Succeed();
    }

    private bool canUseAsChangedIntermediateApprover(ApprovalRequest request, UserAccount nextApprover)
    {
        if (nextApprover.Id == request.RequesterUserId)
        {
            return false;
        }

        EUserRole requesterRole = getRequesterRole(request);
        return UserPermissionPolicy.CanUseAsIntermediateApprovalApprover(requesterRole, nextApprover.Role);
    }
}
