using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalRequestItem
{
    public int ID
    {
        get; set;
    }
    public string Title { get; set; } = string.Empty;
    public string FileID { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ApprovedFileID { get; set; } = string.Empty;
    public string ApprovedFolderDisplayName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
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

    public string GetDownloadFileID()
    {
        if (string.IsNullOrWhiteSpace(ApprovedFileID))
        {
            return FileID;
        }

        return ApprovedFileID;
    }
}
