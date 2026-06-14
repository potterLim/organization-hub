using System;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private ApprovalRequest createApprovalRequest(
        UserSession userSession,
        ApprovalRequestCommand command,
        DriveFolderReference sourceFolder,
        DriveFileMetadata fileMetadata,
        DriveFileMetadata destinationFolderMetadata)
    {
        ApprovalRequest request = new ApprovalRequest();
        request.Title = getRequestTitle(command.Title, fileMetadata.Name);
        request.FileId = fileMetadata.Id;
        request.FileName = fileMetadata.Name;
        request.MimeType = fileMetadata.MimeType;
        request.SourceFolderId = sourceFolder.FolderId.GetRequiredValue();
        request.SourceFolderKind = sourceFolder.FolderKind;
        request.TeamId = sourceFolder.TeamIdValueOrNull;
        request.TeamName = getSourceFolderOwnerName(sourceFolder);
        request.RequesterUserId = userSession.UserId;
        request.RequesterDisplayName = userSession.DisplayName;
        request.RequestedAt = DateTime.UtcNow;
        request.Status = EApprovalRequestStatus.Pending;
        request.StepCount = command.StepCount.Value;
        request.CurrentStepNumber = 1;
        request.ApprovedFileId = string.Empty;
        request.ApprovedFolderId = command.DestinationFolderId.GetRequiredValue();
        request.ApprovedFolderDisplayName = destinationFolderMetadata.Name;
        return request;
    }

    private ApprovalStep createApprovalStep(int stepNumber, ApprovalUserOption approver)
    {
        ApprovalStep step = new ApprovalStep();
        step.StepNumber = stepNumber;
        step.ApproverUserId = approver.UserId;
        step.ApproverDisplayName = approver.DisplayName;
        step.Status = EApprovalStepStatus.Pending;
        step.Comment = string.Empty;
        return step;
    }

    private string getRequestTitle(string title, string fileName)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return fileName;
        }

        return title.Trim();
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
