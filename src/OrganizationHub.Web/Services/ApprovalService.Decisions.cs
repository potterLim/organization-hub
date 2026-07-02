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
        ApprovalComment comment,
        CancellationToken cancellationToken
    )
    {
        return await ApproveRequestAsync(
            userSession,
            approvalRequestId,
            nextApproverUserId,
            comment,
            EDriveFileNameConflictResolution.AddNewFile,
            cancellationToken);
    }

    public async Task<ServiceResult> ApproveRequestAsync(
        UserSession userSession,
        ApprovalRequestId approvalRequestId,
        ApprovalUserId nextApproverUserId,
        ApprovalComment comment,
        EDriveFileNameConflictResolution approvedFileConflictResolution,
        CancellationToken cancellationToken
    )
    {
        ServiceResult commentValidationResult = validateApprovalComment(comment);
        if (commentValidationResult.IsSuccess == false)
        {
            return commentValidationResult;
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
                return ServiceResult.Fail("결재할 권한이 없습니다.");
            }

            UserAccount? actorUserOrNull = await loadUserAccountWithAffiliationOrNullAsync(
                dbContext,
                userSession.UserAccountId,
                cancellationToken);
            ApprovalNextApproverChangeRequest changeRequest = new ApprovalNextApproverChangeRequest
            {
                DbContext = dbContext,
                ApprovalRequest = requestOrNull,
                UserSession = userSession,
                ActorUserOrNull = actorUserOrNull,
                NextApproverUserId = nextApproverUserId,
                CancellationToken = cancellationToken
            };
            ServiceResult changeResult = await changeNextApproverIfRequestedAsync(changeRequest);
            if (changeResult.IsSuccess == false)
            {
                return changeResult;
            }

            if (currentStepOrNull.StepNumber == requestOrNull.StepCount)
            {
                ServiceResult copyResult = await copyApprovedFileIfConfiguredAsync(dbContext, requestOrNull, approvedFileConflictResolution, cancellationToken);
                if (copyResult.IsSuccess == false)
                {
                    return copyResult;
                }
            }

            ApprovalComment approvalComment = getApprovalCommentOrDefault(requestOrNull, currentStepOrNull, comment);
            approveCurrentStep(userSession, actorUserOrNull, requestOrNull, currentStepOrNull, approvalComment);
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> RejectRequestAsync(
        UserSession userSession,
        ApprovalRequestId approvalRequestId,
        ApprovalComment comment,
        CancellationToken cancellationToken
    )
    {
        if (comment.HasValue == false)
        {
            return ServiceResult.Fail("반려 코멘트를 입력해 주세요.");
        }

        ServiceResult commentValidationResult = validateApprovalComment(comment);
        if (commentValidationResult.IsSuccess == false)
        {
            return commentValidationResult;
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

            UserAccount? actorUserOrNull = await loadUserAccountWithAffiliationOrNullAsync(
                dbContext,
                userSession.UserAccountId,
                cancellationToken);
            rejectCurrentStep(userSession, actorUserOrNull, requestOrNull, currentStepOrNull, comment);
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    private void approveCurrentStep(
        UserSession userSession,
        UserAccount? actorUserOrNull,
        ApprovalRequest request,
        ApprovalStep currentStep,
        ApprovalComment comment)
    {
        ApprovalStepNumber currentStepNumber = new ApprovalStepNumber(currentStep.StepNumber);
        currentStep.Status = EApprovalStepStatus.Approved;
        currentStep.ActionedAt = DateTime.UtcNow;
        currentStep.Comment = comment.GetOptionalValue();
        request.AuditLogs.Add(createAuditLog(createAuditLogRequest(userSession, actorUserOrNull, EApprovalAuditAction.Approved, currentStepNumber, comment)));
        if (currentStep.StepNumber == request.StepCount)
        {
            request.Status = EApprovalRequestStatus.Approved;
            request.CompletedAt = DateTime.UtcNow;
            return;
        }

        request.CurrentStepNumber = currentStep.StepNumber + 1;
    }

    private static ApprovalComment getApprovalCommentOrDefault(ApprovalRequest request, ApprovalStep currentStep, ApprovalComment comment)
    {
        if (comment.HasValue)
        {
            return comment;
        }

        if (currentStep.StepNumber == request.StepCount)
        {
            return ApprovalComment.CreateOptional("결재");
        }

        return ApprovalComment.CreateOptional("승인");
    }

    private void rejectCurrentStep(
        UserSession userSession,
        UserAccount? actorUserOrNull,
        ApprovalRequest request,
        ApprovalStep currentStep,
        ApprovalComment comment)
    {
        ApprovalStepNumber currentStepNumber = new ApprovalStepNumber(currentStep.StepNumber);
        currentStep.Status = EApprovalStepStatus.Rejected;
        currentStep.ActionedAt = DateTime.UtcNow;
        currentStep.Comment = comment.GetOptionalValue();
        request.Status = EApprovalRequestStatus.Rejected;
        request.RejectedAt = DateTime.UtcNow;
        request.AuditLogs.Add(createAuditLog(createAuditLogRequest(userSession, actorUserOrNull, EApprovalAuditAction.Rejected, currentStepNumber, comment)));
    }

    private ServiceResult validateApprovalComment(ApprovalComment comment)
    {
        if (comment.IsWithinMaximumLength == false)
        {
            return ServiceResult.Fail("코멘트는 1,000자 이내로 입력해 주세요.");
        }

        return ServiceResult.Succeed();
    }
}
