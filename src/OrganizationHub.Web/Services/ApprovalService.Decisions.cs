using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

            if (requestOrNull.Status == EApprovalRequestStatus.Publishing && requestOrNull.Steps.Exists(step => step.StepNumber == requestOrNull.StepCount && step.ApproverUserId == userSession.UserAccountId.GetRequiredValue()))
            {
                return await mPublicationService.PublishApprovedFileAsync(approvalRequestId, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(requestOrNull.DocumentHash))
            {
                return ServiceResult.Fail("제출 당시 문서가 보존되지 않은 기존 요청입니다. 문서를 확인한 뒤 새로 상신해 주세요.");
            }

            ApprovalStep? currentStepOrNull = getCurrentStepOrNull(requestOrNull, userSession);
            if (currentStepOrNull == null)
            {
                return ServiceResult.Fail("결재할 권한이 없습니다.");
            }

            if (await canActionApprovalStepAsync(userSession, requestOrNull, currentStepOrNull, cancellationToken) == false)
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
                ServiceResult copyResult = await preparePublicationAsync(dbContext, requestOrNull, approvedFileConflictResolution, cancellationToken);
                if (copyResult.IsSuccess == false)
                {
                    return copyResult;
                }
            }

            ApprovalComment approvalComment = getApprovalCommentOrDefault(requestOrNull, currentStepOrNull, comment);
            approveCurrentStep(userSession, actorUserOrNull, requestOrNull, currentStepOrNull, approvalComment);
            bool isFinalStep = currentStepOrNull.StepNumber == requestOrNull.StepCount;
            if (isFinalStep)
            {
                requestOrNull.Status = EApprovalRequestStatus.Publishing;
                requestOrNull.CompletedAt = null;
            }

            ServiceResult saveResult = await saveDecisionAsync(dbContext, requestOrNull, cancellationToken);
            if (saveResult.IsSuccess == false)
            {
                return saveResult;
            }

            bumpApprovalVersion();
            if (isFinalStep)
            {
                return await mPublicationService.PublishApprovedFileAsync(approvalRequestId, cancellationToken);
            }

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

            if (await canActionApprovalStepAsync(userSession, requestOrNull, currentStepOrNull, cancellationToken) == false)
            {
                return ServiceResult.Fail("반려할 권한이 없습니다.");
            }

            UserAccount? actorUserOrNull = await loadUserAccountWithAffiliationOrNullAsync(
                dbContext,
                userSession.UserAccountId,
                cancellationToken);
            rejectCurrentStep(userSession, actorUserOrNull, requestOrNull, currentStepOrNull, comment);
            ServiceResult saveResult = await saveDecisionAsync(dbContext, requestOrNull, cancellationToken);
            if (saveResult.IsSuccess == false)
            {
                return saveResult;
            }

            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    private async Task<ServiceResult> saveDecisionAsync(OrganizationHubDbContext dbContext, ApprovalRequest request, CancellationToken cancellationToken)
    {
        request.Revision++;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("다른 요청에서 이미 처리한 결재입니다. 목록을 새로 확인해 주세요.");
        }
        catch (DbUpdateException exception)
        {
            mLogger.LogWarning(exception, "Approval decision could not be saved.");
            return ServiceResult.Fail("결재를 저장하지 못했습니다. 다른 승인본 저장 작업이 진행 중인지 확인한 뒤 다시 시도해 주세요.");
        }
    }

    private async Task<bool> canActionApprovalStepAsync(
        UserSession userSession,
        ApprovalRequest request,
        ApprovalStep step,
        CancellationToken cancellationToken)
    {
        string requiredPermissionKey = step.StepNumber == request.StepCount ? PermissionKeys.APPROVAL_FINAL_APPROVE : PermissionKeys.APPROVAL_APPROVE;
        OrganizationTeamId? targetTeamIdOrNull = OrganizationTeamId.CreateExistingOrNull(request.TeamId);
        return await mOrganizationPermissionService.HasPermissionAsync(
            userSession,
            new PermissionKey(requiredPermissionKey),
            targetTeamIdOrNull,
            cancellationToken);
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
