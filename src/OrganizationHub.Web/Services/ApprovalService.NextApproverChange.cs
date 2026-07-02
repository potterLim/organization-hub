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
    private async Task<ServiceResult> changeNextApproverIfRequestedAsync(ApprovalNextApproverChangeRequest changeRequest)
    {
        ApprovalRequest request = changeRequest.ApprovalRequest;
        ApprovalUserId nextApproverUserId = changeRequest.NextApproverUserId;
        if (nextApproverUserId.HasValue == false)
        {
            return ServiceResult.Succeed();
        }

        ApprovalStepNumber nextStepNumber = new ApprovalStepNumber(request.CurrentStepNumber + 1);
        int nextStepNumberValue = nextStepNumber.GetRequiredValue();
        ApprovalStep? nextStepOrNull = request.Steps.FirstOrDefault(step => step.StepNumber == nextStepNumberValue);
        if (nextStepOrNull == null)
        {
            return ServiceResult.Fail("다음 결재 단계를 찾을 수 없습니다.");
        }

        ApprovalStepCount requestStepCount = new ApprovalStepCount(request.StepCount);
        if (requestStepCount.ContainsStepNumber(nextStepNumber) == false || nextStepNumber.IsFinalStepOf(requestStepCount))
        {
            if (nextStepOrNull.ApproverUserId == nextApproverUserId.GetRequiredValue())
            {
                return ServiceResult.Succeed();
            }

            return ServiceResult.Fail("최종 결재권자는 변경할 수 없습니다.");
        }

        int requiredNextApproverUserId = nextApproverUserId.GetRequiredValue();
        UserAccount? nextApproverOrNull = await changeRequest.DbContext.UserAccounts
            .Include(user => user.Team)
            .Include(user => user.Position)
            .ThenInclude(position => position!.PermissionGrants)
            .FirstOrDefaultAsync(user => user.Id == requiredNextApproverUserId && user.IsActive, changeRequest.CancellationToken);
        if (nextApproverOrNull == null || canUseAsChangedIntermediateApprover(request, nextApproverOrNull) == false)
        {
            return ServiceResult.Fail("변경할 결재권자를 찾을 수 없습니다.");
        }

        if (nextApproverOrNull.Id == changeRequest.UserSession.UserAccountId.GetRequiredValue())
        {
            return ServiceResult.Fail("다음 결재권자를 자기 자신으로 지정할 수 없습니다.");
        }

        foreach (ApprovalStep step in request.Steps)
        {
            if (step.StepNumber == nextStepNumberValue)
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
        nextStepOrNull.ApproverTeamId = nextApproverOrNull.TeamId;
        nextStepOrNull.ApproverTeamName = getUserTeamName(nextApproverOrNull);
        nextStepOrNull.ApproverPositionId = nextApproverOrNull.PositionId;
        nextStepOrNull.ApproverPositionName = getUserPositionName(nextApproverOrNull, nextApproverOrNull.DisplayName);
        request.AuditLogs.Add(createAuditLog(createApproverChangedAuditLogRequest(
            changeRequest.UserSession,
            changeRequest.ActorUserOrNull,
            nextStepNumber,
            previousApproverDisplayName,
            nextApproverOrNull.DisplayName)));
        return ServiceResult.Succeed();
    }

    private bool canUseAsChangedIntermediateApprover(ApprovalRequest request, UserAccount nextApprover)
    {
        if (nextApprover.Id == request.RequesterUserId)
        {
            return false;
        }

        OrganizationTeamId? requesterTeamIdOrNull = OrganizationTeamId.CreateExistingOrNull(request.TeamId);
        return hasUserApprovalPermission(nextApprover, requesterTeamIdOrNull, new PermissionKey(PermissionKeys.APPROVAL_APPROVE));
    }

    private sealed class ApprovalNextApproverChangeRequest
    {
        public required OrganizationHubDbContext DbContext
        {
            get; init;
        }

        public required ApprovalRequest ApprovalRequest
        {
            get; init;
        }

        public required UserSession UserSession
        {
            get; init;
        }

        public required UserAccount? ActorUserOrNull
        {
            get; init;
        }

        public required ApprovalUserId NextApproverUserId
        {
            get; init;
        }

        public required CancellationToken CancellationToken
        {
            get; init;
        }
    }
}
