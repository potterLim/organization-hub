using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalHistory
{
    private List<ApprovalRequestItem> mApprovalHistory = new List<ApprovalRequestItem>();
    private bool mIsLoading = true;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await tryLoadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        IReadOnlyList<ApprovalRequestItem> approvalHistory = await ApprovalService.GetRelatedHistoryAsync(CurrentUserSessionOrNull, CancellationToken.None);
        mApprovalHistory = approvalHistory.ToList();
        mIsLoading = false;
    }

    private static string getStatusText(ApprovalRequestItem approvalRequest)
    {
        if (approvalRequest.Status == EApprovalRequestStatus.Approved)
        {
            return "완료";
        }

        return "반려";
    }

    private static string getStatusClass(ApprovalRequestItem approvalRequest)
    {
        if (approvalRequest.Status == EApprovalRequestStatus.Approved)
        {
            return "hub-status-pill hub-status-active";
        }

        return "hub-status-pill hub-status-inactive";
    }

    private static string getStepClass(ApprovalStepItem step)
    {
        if (step.Status == EApprovalStepStatus.Approved)
        {
            return "hub-status-pill hub-status-active";
        }

        if (step.Status == EApprovalStepStatus.Rejected)
        {
            return "hub-status-pill hub-status-inactive";
        }

        return "hub-status-pill";
    }

    private static string getStepLabel(ApprovalRequestItem approvalRequest, ApprovalStepItem step)
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        return ApprovalStepLabelUtility.GetStepLabel(stepCount, new ApprovalStepNumber(step.StepNumber));
    }

    private static string getStepDisplayText(ApprovalRequestItem approvalRequest, ApprovalStepItem step)
    {
        return getStepLabel(approvalRequest, step) + ": " + step.GetApproverAffiliationText();
    }

    private static IEnumerable<ApprovalAuditLogItem> getDisplayAuditLogs(ApprovalRequestItem approvalRequest)
    {
        foreach (ApprovalAuditLogItem auditLog in approvalRequest.AuditLogs)
        {
            if (auditLog.Action == EApprovalAuditAction.Completed)
            {
                continue;
            }

            yield return auditLog;
        }
    }

    private static string getAuditActionText(ApprovalRequestItem approvalRequest, ApprovalAuditLogItem auditLog)
    {
        switch (auditLog.Action)
        {
            case EApprovalAuditAction.Submitted:
                return "요청";
            case EApprovalAuditAction.Approved:
                if (isFinalApprovalAction(approvalRequest, auditLog))
                {
                    return "결재";
                }

                return "승인";
            case EApprovalAuditAction.Rejected:
                return "반려";
            case EApprovalAuditAction.ApproverChanged:
                return "결재선 변경";
            case EApprovalAuditAction.Completed:
                return "완료";
            default:
                return "기록";
        }
    }

    private static string getAuditCommentClass(ApprovalAuditLogItem auditLog)
    {
        if (hasDisplayComment(auditLog))
        {
            return "hub-approval-log-comment";
        }

        return "hub-approval-log-comment hub-muted";
    }

    private static string getAuditCommentText(ApprovalAuditLogItem auditLog)
    {
        if (auditLog.Action == EApprovalAuditAction.ApproverChanged && hasApproverChangeDisplayText(auditLog))
        {
            return auditLog.PreviousApproverDisplayName + " → " + auditLog.NewApproverDisplayName;
        }

        if (string.IsNullOrWhiteSpace(auditLog.Comment) == false)
        {
            return auditLog.Comment;
        }

        return "의견 없음";
    }

    private static bool hasDisplayComment(ApprovalAuditLogItem auditLog)
    {
        if (auditLog.Action == EApprovalAuditAction.ApproverChanged)
        {
            return hasApproverChangeDisplayText(auditLog);
        }

        return string.IsNullOrWhiteSpace(auditLog.Comment) == false;
    }

    private static bool hasApproverChangeDisplayText(ApprovalAuditLogItem auditLog)
    {
        return
            string.IsNullOrWhiteSpace(auditLog.PreviousApproverDisplayName) == false &&
            string.IsNullOrWhiteSpace(auditLog.NewApproverDisplayName) == false;
    }

    private static bool isFinalApprovalAction(ApprovalRequestItem approvalRequest, ApprovalAuditLogItem auditLog)
    {
        if (auditLog.Action != EApprovalAuditAction.Approved || auditLog.StepNumber.HasValue == false)
        {
            return false;
        }

        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        ApprovalStepNumber stepNumber = new ApprovalStepNumber(auditLog.StepNumber.GetValueOrDefault());
        return stepNumber.IsFinalStepOf(stepCount);
    }

    private string formatDate(DateTime dateTime)
    {
        return PresentationDateTimeFormatter.formatClientDateTime(dateTime, ClientTimeZoneInfo, PresentationDateTimeFormatter.FULL_DATE_TIME_FORMAT);
    }
}
