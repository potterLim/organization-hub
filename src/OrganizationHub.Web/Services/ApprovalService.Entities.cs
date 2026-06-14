using System;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private ApprovalRequest createApprovalRequest(UserSession userSession, ApprovalRequestFormModel formModel, DriveFolderReference sourceFolder, DriveFileMetadata fileMetadata, DriveFileMetadata destinationFolderMetadata)
    {
        ApprovalRequest request = new ApprovalRequest();
        request.Title = getRequestTitle(formModel.Title, fileMetadata.Name);
        request.FileID = fileMetadata.ID;
        request.FileName = fileMetadata.Name;
        request.MimeType = fileMetadata.MimeType;
        request.SourceFolderID = sourceFolder.FolderID;
        request.SourceFolderKind = sourceFolder.FolderKind;
        request.TeamID = sourceFolder.TeamID;
        request.TeamName = getSourceFolderOwnerName(sourceFolder);
        request.RequesterUserID = userSession.UserID;
        request.RequesterDisplayName = userSession.DisplayName;
        request.RequestedAt = DateTime.UtcNow;
        request.Status = EApprovalRequestStatus.Pending;
        request.StepCount = formModel.StepCount;
        request.CurrentStepNumber = 1;
        request.ApprovedFileID = string.Empty;
        request.ApprovedFolderID = formModel.DestinationFolderID.Trim();
        request.ApprovedFolderDisplayName = destinationFolderMetadata.Name;
        return request;
    }

    private ApprovalStep createApprovalStep(int stepNumber, ApprovalUserOption approver)
    {
        ApprovalStep step = new ApprovalStep();
        step.StepNumber = stepNumber;
        step.ApproverUserID = approver.UserID;
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
