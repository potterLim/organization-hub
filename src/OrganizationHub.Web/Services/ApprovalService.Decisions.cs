using System;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    public async Task<ServiceResult> ApproveRequestAsync(
        UserSession userSession,
        ApprovalRequestId approvalRequestId,
        ApprovalUserId nextApproverUserId,
        string comment,
        CancellationToken cancellationToken
    )
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalRequest? requestOrNull = await loadApprovalRequestOrNullAsync(dbContext, approvalRequestId, cancellationToken);
            if (requestOrNull == null)
            {
                return ServiceResult.Fail("결재 요청을 찾을 수 없습니다.");
            }

            ApprovalStep? currentStepOrNull = getCurrentStepOrNull(requestOrNull, userSession);
            if (currentStepOrNull == null)
            {
                return ServiceResult.Fail("결재할 권한이 없습니다.");
            }

            ServiceResult changeResult = await changeNextApproverIfRequestedAsync(dbContext, requestOrNull, userSession, nextApproverUserId, cancellationToken);
            if (changeResult.IsSuccess == false)
            {
                return changeResult;
            }

            if (currentStepOrNull.StepNumber == requestOrNull.StepCount)
            {
                ServiceResult copyResult = await copyApprovedFileIfConfiguredAsync(dbContext, requestOrNull, cancellationToken);
                if (copyResult.IsSuccess == false)
                {
                    return copyResult;
                }
            }

            approveCurrentStep(userSession, requestOrNull, currentStepOrNull, comment);
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> RejectRequestAsync(
        UserSession userSession,
        ApprovalRequestId approvalRequestId,
        string comment,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return ServiceResult.Fail("반려 코멘트를 입력해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalRequest? requestOrNull = await loadApprovalRequestOrNullAsync(dbContext, approvalRequestId, cancellationToken);
            if (requestOrNull == null)
            {
                return ServiceResult.Fail("결재 요청을 찾을 수 없습니다.");
            }

            ApprovalStep? currentStepOrNull = getCurrentStepOrNull(requestOrNull, userSession);
            if (currentStepOrNull == null)
            {
                return ServiceResult.Fail("반려할 권한이 없습니다.");
            }

            rejectCurrentStep(userSession, requestOrNull, currentStepOrNull, comment);
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    private void approveCurrentStep(UserSession userSession, ApprovalRequest request, ApprovalStep currentStep, string comment)
    {
        currentStep.Status = EApprovalStepStatus.Approved;
        currentStep.ActionedAt = DateTime.UtcNow;
        currentStep.Comment = getTrimmedComment(comment);
        request.AuditLogs.Add(createAuditLog(createAuditLogRequest(userSession, EApprovalAuditAction.Approved, currentStep.StepNumber, currentStep.Comment)));
        if (currentStep.StepNumber == request.StepCount)
        {
            request.Status = EApprovalRequestStatus.Approved;
            request.CompletedAt = DateTime.UtcNow;
            request.AuditLogs.Add(createAuditLog(createAuditLogRequest(userSession, EApprovalAuditAction.Completed, currentStep.StepNumber, "최종 결재 완료")));
            return;
        }

        request.CurrentStepNumber = currentStep.StepNumber + 1;
    }

    private void rejectCurrentStep(UserSession userSession, ApprovalRequest request, ApprovalStep currentStep, string comment)
    {
        currentStep.Status = EApprovalStepStatus.Rejected;
        currentStep.ActionedAt = DateTime.UtcNow;
        currentStep.Comment = getTrimmedComment(comment);
        request.Status = EApprovalRequestStatus.Rejected;
        request.RejectedAt = DateTime.UtcNow;
        request.AuditLogs.Add(createAuditLog(createAuditLogRequest(userSession, EApprovalAuditAction.Rejected, currentStep.StepNumber, currentStep.Comment)));
    }
}
