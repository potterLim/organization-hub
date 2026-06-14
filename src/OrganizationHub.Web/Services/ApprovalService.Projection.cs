using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private async Task<List<ApprovalRequest>> filterApprovedRequestsByDestinationAsync(
        UserSession userSession,
        IReadOnlyList<ApprovalRequest> requests,
        CancellationToken cancellationToken
    )
    {
        List<ApprovalRequest> readableRequests = new List<ApprovalRequest>();
        foreach (ApprovalRequest request in requests)
        {
            bool canReadDestination = await canReadApprovedRequestDestinationAsync(userSession, request, cancellationToken);
            if (canReadDestination)
            {
                readableRequests.Add(request);
            }
        }

        return readableRequests;
    }

    private async Task<bool> canReadApprovedRequestDestinationAsync(
        UserSession userSession,
        ApprovalRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedFolderId))
        {
            return false;
        }

        return await mPermissionService.CanStoreApprovedFileToFolderAsync(userSession, DriveFolderId.CreateOptional(request.ApprovedFolderId), cancellationToken);
    }

    private ApprovalAuditLog createAuditLog(ApprovalAuditLogCreationRequest request)
    {
        ApprovalAuditLog auditLog = new ApprovalAuditLog();
        auditLog.ActorUserId = request.UserSession.UserId;
        auditLog.ActorDisplayName = request.UserSession.DisplayName;
        auditLog.Action = request.Action;
        auditLog.StepNumber = request.StepNumberOrNull;
        auditLog.Comment = getTrimmedComment(request.Comment);
        auditLog.PreviousApproverDisplayName = request.PreviousApproverDisplayName;
        auditLog.NewApproverDisplayName = request.NewApproverDisplayName;
        auditLog.CreatedAt = DateTime.UtcNow;
        return auditLog;
    }

    private ApprovalAuditLogCreationRequest createAuditLogRequest(
        UserSession userSession,
        EApprovalAuditAction action,
        int? stepNumberOrNull,
        string comment)
    {
        return new ApprovalAuditLogCreationRequest(userSession, action, stepNumberOrNull, comment);
    }

    private ApprovalAuditLogCreationRequest createApproverChangedAuditLogRequest(
        UserSession userSession,
        int stepNumber,
        string previousApproverDisplayName,
        string newApproverDisplayName)
    {
        ApprovalAuditLogCreationRequest request = createAuditLogRequest(userSession, EApprovalAuditAction.ApproverChanged, stepNumber, "결재선 변경");
        request.PreviousApproverDisplayName = previousApproverDisplayName;
        request.NewApproverDisplayName = newApproverDisplayName;
        return request;
    }

    private string getTrimmedComment(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        return comment.Trim();
    }

    private sealed class ApprovalAuditLogCreationRequest
    {
        public UserSession UserSession
        {
            get; private init;
        }
        public EApprovalAuditAction Action
        {
            get; private init;
        }
        public int? StepNumberOrNull
        {
            get; private init;
        }
        public string Comment { get; private init; } = string.Empty;
        public string PreviousApproverDisplayName { get; set; } = string.Empty;
        public string NewApproverDisplayName { get; set; } = string.Empty;

        public ApprovalAuditLogCreationRequest(UserSession userSession, EApprovalAuditAction action, int? stepNumberOrNull, string comment)
        {
            UserSession = userSession;
            Action = action;
            StepNumberOrNull = stepNumberOrNull;
            Comment = comment;
        }
    }

    private IReadOnlyList<ApprovalRequestItem> createApprovalRequestItems(IReadOnlyList<ApprovalRequest> requests)
    {
        List<ApprovalRequestItem> items = new List<ApprovalRequestItem>();
        foreach (ApprovalRequest request in requests)
        {
            items.Add(createApprovalRequestItem(request));
        }

        return items;
    }

    private ApprovalRequestItem createApprovalRequestItem(ApprovalRequest request)
    {
        ApprovalRequestItem item = new ApprovalRequestItem();
        item.Id = request.Id;
        item.Title = request.Title;
        item.FileId = request.FileId;
        item.FileName = request.FileName;
        item.ApprovedFileId = request.ApprovedFileId;
        item.ApprovedFolderDisplayName = request.ApprovedFolderDisplayName;
        item.TeamName = request.TeamName;
        item.RequesterDisplayName = request.RequesterDisplayName;
        item.RequestedAt = request.RequestedAt;
        item.Status = request.Status;
        item.StepCount = request.StepCount;
        item.CurrentStepNumber = request.CurrentStepNumber;
        item.CompletedAt = request.CompletedAt;
        item.RejectedAt = request.RejectedAt;
        item.Steps = request.Steps.OrderBy(step => step.StepNumber).Select(createApprovalStepItem).ToList();
        item.AuditLogs = request.AuditLogs.OrderByDescending(auditLog => auditLog.CreatedAt).Select(createApprovalAuditLogItem).ToList();
        return item;
    }

    private ApprovalStepItem createApprovalStepItem(ApprovalStep step)
    {
        ApprovalStepItem item = new ApprovalStepItem();
        item.StepNumber = step.StepNumber;
        item.ApproverUserId = step.ApproverUserId;
        item.ApproverDisplayName = step.ApproverDisplayName;
        item.Status = step.Status;
        item.ActionedAt = step.ActionedAt;
        item.Comment = step.Comment;
        return item;
    }

    private ApprovalAuditLogItem createApprovalAuditLogItem(ApprovalAuditLog auditLog)
    {
        ApprovalAuditLogItem item = new ApprovalAuditLogItem();
        item.Action = auditLog.Action;
        item.ActorDisplayName = auditLog.ActorDisplayName;
        item.StepNumber = auditLog.StepNumber;
        item.Comment = auditLog.Comment;
        item.PreviousApproverDisplayName = auditLog.PreviousApproverDisplayName;
        item.NewApproverDisplayName = auditLog.NewApproverDisplayName;
        item.CreatedAt = auditLog.CreatedAt;
        return item;
    }
}
