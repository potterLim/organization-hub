using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalRequestItem
{
    public bool IsPublicationProcessing { get; set; }
    public bool HasPublicationFailure { get; set; }

    public int RequesterUserId
    {
        get; set;
    }
    public bool HasDocumentSnapshot
    {
        get; set;
    }
    public int Id
    {
        get; set;
    }
    public string Title { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ApprovedFileId { get; set; } = string.Empty;
    public string ApprovedFolderId { get; set; } = string.Empty;
    public string ApprovedFolderDisplayName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string RequesterTeamName { get; set; } = string.Empty;
    public string RequesterPositionName { get; set; } = string.Empty;
    public string RequesterDisplayName { get; set; } = string.Empty;
    public DateTime RequestedAt
    {
        get; set;
    }
    public EApprovalRequestStatus Status
    {
        get; set;
    }
    public int StepCount
    {
        get; set;
    }
    public int CurrentStepNumber
    {
        get; set;
    }
    public DateTime? CompletedAt
    {
        get; set;
    }
    public DateTime? RejectedAt
    {
        get; set;
    }
    public IReadOnlyList<ApprovalStepItem> Steps { get; set; } = new List<ApprovalStepItem>();
    public IReadOnlyList<ApprovalAuditLogItem> AuditLogs { get; set; } = new List<ApprovalAuditLogItem>();

    public string GetDownloadFileId()
    {
        if (string.IsNullOrWhiteSpace(ApprovedFileId))
        {
            return FileId;
        }

        return ApprovedFileId;
    }

    public string GetRequesterAffiliationText()
    {
        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(RequesterTeamName, RequesterPositionName);
        string affiliationText = affiliationLabel.GetFullText();
        if (string.IsNullOrWhiteSpace(affiliationText))
        {
            return RequesterDisplayName;
        }

        return affiliationText;
    }
}
