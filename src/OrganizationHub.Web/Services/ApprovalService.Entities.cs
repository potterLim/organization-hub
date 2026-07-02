using System;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private ApprovalRequest createApprovalRequest(ValidatedApprovalSubmission submission)
    {
        ApprovalRequest request = new ApprovalRequest();
        request.Title = getRequestTitle(submission.Command.Title, DriveItemName.CreateOptional(submission.FileMetadata.Name));
        request.FileId = submission.FileMetadata.Id;
        request.FileName = submission.FileMetadata.Name;
        request.MimeType = submission.FileMetadata.MimeType;
        request.SourceFolderId = submission.SourceFolder.FolderId.GetRequiredValue();
        request.SourceFolderKind = submission.SourceFolder.FolderKind;
        request.TeamId = submission.SourceFolder.TeamIdValueOrNull;
        request.TeamName = getSourceFolderOwnerName(submission.SourceFolder);
        request.RequesterUserId = submission.UserSession.UserAccountId.GetRequiredValue();
        request.RequesterDisplayName = submission.UserSession.DisplayName;
        request.RequesterTeamId = getUserTeamIdOrNull(submission.RequesterUserOrNull, submission.UserSession);
        request.RequesterTeamName = getUserTeamName(submission.RequesterUserOrNull, submission.UserSession.TeamDisplayName);
        request.RequesterPositionId = getUserPositionIdOrNull(submission.RequesterUserOrNull);
        request.RequesterPositionName = getUserPositionName(submission.RequesterUserOrNull, submission.UserSession.DisplayName);
        request.RequestedAt = DateTime.UtcNow;
        request.Status = EApprovalRequestStatus.Pending;
        request.StepCount = submission.Command.StepCount.GetRequiredValue();
        request.CurrentStepNumber = ApprovalStepNumber.CreateFirstApproverStep().GetRequiredValue();
        request.ApprovedFileId = string.Empty;
        request.ApprovedFolderId = submission.Command.DestinationFolderId.GetRequiredValue();
        request.ApprovedFolderDisplayName = submission.DestinationFolderMetadata.Name;
        return request;
    }

    private ApprovalStep createApprovalStep(ApprovalStepNumber stepNumber, ApprovalUserOption approver)
    {
        ApprovalStep step = new ApprovalStep();
        step.StepNumber = stepNumber.GetRequiredValue();
        step.ApproverUserId = approver.UserId;
        step.ApproverDisplayName = approver.DisplayName;
        step.ApproverTeamId = approver.TeamId;
        step.ApproverTeamName = approver.TeamName;
        step.ApproverPositionId = approver.PositionId;
        step.ApproverPositionName = approver.PositionName;
        step.Status = EApprovalStepStatus.Pending;
        step.Comment = string.Empty;
        return step;
    }

    private string getRequestTitle(ApprovalRequestTitle title, DriveItemName fallbackFileName)
    {
        return title.GetValueOrFallback(fallbackFileName);
    }

    private string getSourceFolderOwnerName(DriveFolderReference sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder.TeamName) == false)
        {
            return sourceFolder.TeamName;
        }

        return string.Empty;
    }
}
