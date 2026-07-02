using System;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private ApprovalAuditLog createAuditLog(ApprovalAuditLogCreationRequest request)
    {
        ApprovalAuditLog auditLog = new ApprovalAuditLog();
        auditLog.ActorUserId = request.UserSession.UserAccountId.GetRequiredValue();
        auditLog.ActorDisplayName = request.UserSession.DisplayName;
        auditLog.ActorTeamId = getUserTeamIdOrNull(request.ActorUserOrNull, request.UserSession);
        auditLog.ActorTeamName = getUserTeamName(request.ActorUserOrNull, request.UserSession.TeamDisplayName);
        auditLog.ActorPositionId = getUserPositionIdOrNull(request.ActorUserOrNull);
        auditLog.ActorPositionName = getUserPositionName(request.ActorUserOrNull, request.UserSession.DisplayName);
        auditLog.Action = request.Action;
        auditLog.StepNumber = ApprovalStepNumber.GetValueOrNull(request.StepNumberOrNull);
        auditLog.Comment = request.Comment.GetOptionalValue();
        auditLog.PreviousApproverDisplayName = request.PreviousApproverDisplayName;
        auditLog.NewApproverDisplayName = request.NewApproverDisplayName;
        auditLog.CreatedAt = DateTime.UtcNow;
        return auditLog;
    }

    private ApprovalAuditLogCreationRequest createAuditLogRequest(
        UserSession userSession,
        UserAccount? actorUserOrNull,
        EApprovalAuditAction action,
        ApprovalStepNumber? stepNumberOrNull,
        ApprovalComment comment)
    {
        return new ApprovalAuditLogCreationRequest(userSession, actorUserOrNull, action, stepNumberOrNull, comment);
    }

    private ApprovalAuditLogCreationRequest createApproverChangedAuditLogRequest(
        UserSession userSession,
        UserAccount? actorUserOrNull,
        ApprovalStepNumber stepNumber,
        string previousApproverDisplayName,
        string newApproverDisplayName)
    {
        ApprovalAuditLogCreationRequest request = createAuditLogRequest(
            userSession,
            actorUserOrNull,
            EApprovalAuditAction.ApproverChanged,
            stepNumber,
            ApprovalComment.CreateOptional("결재선 변경"));
        request.PreviousApproverDisplayName = previousApproverDisplayName;
        request.NewApproverDisplayName = newApproverDisplayName;
        return request;
    }

    private sealed class ApprovalAuditLogCreationRequest
    {
        public UserSession UserSession
        {
            get; private init;
        }
        public UserAccount? ActorUserOrNull
        {
            get; private init;
        }
        public EApprovalAuditAction Action
        {
            get; private init;
        }
        public ApprovalStepNumber? StepNumberOrNull
        {
            get; private init;
        }
        public ApprovalComment Comment
        {
            get; private init;
        }
        public string PreviousApproverDisplayName { get; set; } = string.Empty;
        public string NewApproverDisplayName { get; set; } = string.Empty;

        public ApprovalAuditLogCreationRequest(
            UserSession userSession,
            UserAccount? actorUserOrNull,
            EApprovalAuditAction action,
            ApprovalStepNumber? stepNumberOrNull,
            ApprovalComment comment)
        {
            UserSession = userSession;
            ActorUserOrNull = actorUserOrNull;
            Action = action;
            StepNumberOrNull = stepNumberOrNull;
            Comment = comment;
        }
    }
}
