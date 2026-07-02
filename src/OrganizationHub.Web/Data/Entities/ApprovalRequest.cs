using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class ApprovalRequest
{
    public int Id
    {
        get; set;
    }
    public string Title { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string SourceFolderId { get; set; } = string.Empty;
    public EDriveFolderKind SourceFolderKind
    {
        get; set;
    }
    public int? TeamId
    {
        get; set;
    }
    public Team? Team
    {
        get; set;
    }
    public string TeamName { get; set; } = string.Empty;
    public int RequesterUserId
    {
        get; set;
    }
    public UserAccount? RequesterUser
    {
        get; set;
    }
    public string RequesterDisplayName { get; set; } = string.Empty;
    public int? RequesterTeamId
    {
        get; set;
    }
    public string RequesterTeamName { get; set; } = string.Empty;
    public int? RequesterPositionId
    {
        get; set;
    }
    public string RequesterPositionName { get; set; } = string.Empty;
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
    public string ApprovedFileId { get; set; } = string.Empty;
    public string ApprovedFolderId { get; set; } = string.Empty;
    public string ApprovedFolderDisplayName { get; set; } = string.Empty;
    public DateTime? CompletedAt
    {
        get; set;
    }
    public DateTime? RejectedAt
    {
        get; set;
    }
    public List<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();
    public List<ApprovalAuditLog> AuditLogs { get; set; } = new List<ApprovalAuditLog>();
}
