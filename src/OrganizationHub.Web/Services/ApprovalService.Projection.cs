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
    private async Task<List<ApprovalRequest>> filterApprovedRequestsByDestinationAsync(UserSession userSession, IReadOnlyList<ApprovalRequest> requests, CancellationToken cancellationToken)
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

    private async Task<bool> canReadApprovedRequestDestinationAsync(UserSession userSession, ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedFolderID))
        {
            return false;
        }

        return await mPermissionService.CanStoreApprovedFileToFolderAsync(userSession, request.ApprovedFolderID, cancellationToken);
    }

    private ApprovalAuditLog createAuditLog(UserSession userSession, EApprovalAuditAction action, int? stepNumber, string comment, string previousApproverDisplayName, string newApproverDisplayName)
    {
        ApprovalAuditLog auditLog = new ApprovalAuditLog();
        auditLog.ActorUserID = userSession.UserID;
        auditLog.ActorDisplayName = userSession.DisplayName;
        auditLog.Action = action;
        auditLog.StepNumber = stepNumber;
        auditLog.Comment = getTrimmedComment(comment);
        auditLog.PreviousApproverDisplayName = previousApproverDisplayName;
        auditLog.NewApproverDisplayName = newApproverDisplayName;
        auditLog.CreatedAt = DateTime.UtcNow;
        return auditLog;
    }

    private string getTrimmedComment(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        return comment.Trim();
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
        item.ID = request.ID;
        item.Title = request.Title;
        item.FileID = request.FileID;
        item.FileName = request.FileName;
        item.ApprovedFileID = request.ApprovedFileID;
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
        item.ApproverUserID = step.ApproverUserID;
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
