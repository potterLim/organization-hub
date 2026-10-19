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

    private IReadOnlyList<ApprovalRequestItem> createApprovalRequestItems(
        IReadOnlyList<ApprovalRequest> requests,
        IReadOnlyDictionary<int, UserAccount> actorUsersById)
    {
        List<ApprovalRequestItem> items = new List<ApprovalRequestItem>();
        foreach (ApprovalRequest request in requests)
        {
            items.Add(createApprovalRequestItem(request, actorUsersById));
        }

        return items;
    }

    private ApprovalRequestItem createApprovalRequestItem(
        ApprovalRequest request,
        IReadOnlyDictionary<int, UserAccount> actorUsersById)
    {
        ApprovalRequestItem item = new ApprovalRequestItem();
        item.Id = request.Id;
        item.RequesterUserId = request.RequesterUserId;
        item.HasDocumentSnapshot = string.IsNullOrWhiteSpace(request.DocumentHash) == false;
        item.Title = request.Title;
        item.FileId = request.FileId;
        item.FileName = request.FileName;
        item.ApprovedFileId = request.ApprovedFileId;
        item.ApprovedFolderId = request.ApprovedFolderId;
        item.ApprovedFolderDisplayName = request.ApprovedFolderDisplayName;
        item.TeamName = request.TeamName;
        item.RequesterTeamName = getSnapshotText(request.RequesterTeamName, getUserTeamName(request.RequesterUser));
        item.RequesterPositionName = getSnapshotText(request.RequesterPositionName, getUserPositionName(request.RequesterUser, request.RequesterDisplayName));
        item.RequesterDisplayName = request.RequesterDisplayName;
        item.RequestedAt = request.RequestedAt;
        item.Status = request.Status;
        item.StepCount = request.StepCount;
        item.CurrentStepNumber = request.CurrentStepNumber;
        item.CompletedAt = request.CompletedAt;
        item.RejectedAt = request.RejectedAt;
        item.Steps = request.Steps.OrderBy(step => step.StepNumber).Select(createApprovalStepItem).ToList();
        item.AuditLogs = request.AuditLogs
            .OrderByDescending(auditLog => auditLog.CreatedAt)
            .Select(auditLog => createApprovalAuditLogItem(auditLog, actorUsersById))
            .ToList();
        return item;
    }

    private ApprovalStepItem createApprovalStepItem(ApprovalStep step)
    {
        ApprovalStepItem item = new ApprovalStepItem();
        item.StepNumber = step.StepNumber;
        item.ApproverUserId = step.ApproverUserId;
        item.ApproverTeamName = getSnapshotText(step.ApproverTeamName, getUserTeamName(step.ApproverUser));
        item.ApproverPositionName = getSnapshotText(step.ApproverPositionName, getUserPositionName(step.ApproverUser, step.ApproverDisplayName));
        item.ApproverDisplayName = step.ApproverDisplayName;
        item.Status = step.Status;
        item.ActionedAt = step.ActionedAt;
        item.Comment = step.Comment;
        return item;
    }

    private ApprovalAuditLogItem createApprovalAuditLogItem(
        ApprovalAuditLog auditLog,
        IReadOnlyDictionary<int, UserAccount> actorUsersById)
    {
        UserAccount? actorUserOrNull = findActorUserOrNull(auditLog, actorUsersById);
        ApprovalAuditLogItem item = new ApprovalAuditLogItem();
        item.Action = auditLog.Action;
        item.ActorDisplayName = auditLog.ActorDisplayName;
        item.ActorTeamName = getSnapshotText(auditLog.ActorTeamName, getUserTeamName(actorUserOrNull));
        item.ActorPositionName = getSnapshotText(auditLog.ActorPositionName, getUserPositionName(actorUserOrNull, auditLog.ActorDisplayName));
        item.StepNumber = auditLog.StepNumber;
        item.Comment = auditLog.Comment;
        item.PreviousApproverDisplayName = auditLog.PreviousApproverDisplayName;
        item.NewApproverDisplayName = auditLog.NewApproverDisplayName;
        item.CreatedAt = auditLog.CreatedAt;
        return item;
    }

    private static UserAccount? findActorUserOrNull(
        ApprovalAuditLog auditLog,
        IReadOnlyDictionary<int, UserAccount> actorUsersById)
    {
        UserAccount? actorUserOrNull;
        if (actorUsersById.TryGetValue(auditLog.ActorUserId, out actorUserOrNull))
        {
            return actorUserOrNull;
        }

        return null;
    }

    private static string getUserTeamName(UserAccount? userOrNull)
    {
        if (userOrNull == null || userOrNull.Team == null)
        {
            return string.Empty;
        }

        return userOrNull.Team.DisplayName;
    }

    private static int? getUserTeamIdOrNull(
        UserAccount? userOrNull,
        UserSession userSession)
    {
        if (userOrNull != null)
        {
            return userOrNull.TeamId;
        }

        return OrganizationTeamId.GetValueOrNull(userSession.OrganizationTeamIdOrNull);
    }

    private static string getUserTeamName(
        UserAccount? userOrNull,
        string fallbackTeamName)
    {
        string teamName = getUserTeamName(userOrNull);
        if (string.IsNullOrWhiteSpace(teamName) == false)
        {
            return teamName;
        }

        if (string.IsNullOrWhiteSpace(fallbackTeamName))
        {
            return string.Empty;
        }

        return fallbackTeamName.Trim();
    }

    private static int? getUserPositionIdOrNull(UserAccount? userOrNull)
    {
        if (userOrNull == null)
        {
            return null;
        }

        return userOrNull.PositionId;
    }

    private static string getUserPositionName(
        UserAccount? userOrNull,
        string fallbackDisplayName)
    {
        if (userOrNull != null && userOrNull.Position != null)
        {
            return userOrNull.Position.Name;
        }

        if (string.IsNullOrWhiteSpace(fallbackDisplayName))
        {
            return string.Empty;
        }

        return fallbackDisplayName.Trim();
    }

    private static string getSnapshotText(string snapshotText, string fallbackText)
    {
        if (string.IsNullOrWhiteSpace(snapshotText) == false)
        {
            return snapshotText.Trim();
        }

        if (string.IsNullOrWhiteSpace(fallbackText))
        {
            return string.Empty;
        }

        return fallbackText.Trim();
    }
}
