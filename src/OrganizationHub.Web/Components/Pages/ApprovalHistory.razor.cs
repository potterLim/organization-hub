using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalHistory
{
    private List<ApprovalRequestItem> mApprovalHistory = new List<ApprovalRequestItem>();
    private bool mIsLoading = true;
    private bool mShowRequestedOnly;
    private int mMyOngoingCount;
    private int mPageNumber = 1;
    private int mTotalPages = 1;
    private int mTotalCount;
    private string mSearchKeyword = string.Empty;
    private string mErrorMessage = string.Empty;
    private EApprovalHistoryFilter mHistoryFilter = EApprovalHistoryFilter.AllRelated;
    private DateRange? mRequestedDateRangeOrNull;

    [SupplyParameterFromQuery(Name = "view")]
    public string? ViewOrNull
    {
        get; set;
    }

    protected override async Task OnInitializedAsync()
    {
        await tryLoadUserSessionAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        mShowRequestedOnly = ViewOrNull == "requested";
        mHistoryFilter = mShowRequestedOnly ? EApprovalHistoryFilter.MyOngoing : EApprovalHistoryFilter.AllRelated;
        mPageNumber = 1;
        await reloadAsync();
    }

    private async Task reloadAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        mIsLoading = true;
        mErrorMessage = string.Empty;
        try
        {
            DateTime? fromUtcOrNull = mRequestedDateRangeOrNull?.Start == null ? null : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(mRequestedDateRangeOrNull.Start.Value.Date, DateTimeKind.Unspecified), ClientTimeZoneInfo);
            DateTime? toUtcOrNull = mRequestedDateRangeOrNull?.End == null ? null : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(mRequestedDateRangeOrNull.End.Value.Date.AddDays(1), DateTimeKind.Unspecified), ClientTimeZoneInfo);
            ApprovalHistoryQuery query = new ApprovalHistoryQuery
            {
                PageNumber = mPageNumber,
                Keyword = mSearchKeyword,
                Filter = mHistoryFilter,
                RequestedFromUtcOrNull = fromUtcOrNull,
                RequestedToUtcExclusiveOrNull = toUtcOrNull
            };
            ApprovalHistoryPage page = await ApprovalService.GetHistoryPageAsync(CurrentUserSessionOrNull, query, CancellationToken.None);
            mApprovalHistory = page.Items.ToList();
            mPageNumber = page.PageNumber;
            mTotalPages = page.TotalPages;
            mTotalCount = page.TotalCount;
            mMyOngoingCount = page.MyOngoingCount;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Approval history load failed.");
            mErrorMessage = "결재 기록을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }
        finally
        {
            mIsLoading = false;
        }
    }

    private async Task searchAsync()
    {
        mPageNumber = 1;
        await reloadAsync();
    }

    private async Task handleSearchKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await searchAsync();
        }
    }

    private async Task handleFilterChangedAsync(EApprovalHistoryFilter filter)
    {
        mHistoryFilter = filter;
        mShowRequestedOnly = filter == EApprovalHistoryFilter.MyOngoing;
        await searchAsync();
    }

    private async Task previousPageAsync()
    {
        mPageNumber = Math.Max(1, mPageNumber - 1);
        await reloadAsync();
    }

    private async Task nextPageAsync()
    {
        mPageNumber = Math.Min(mTotalPages, mPageNumber + 1);
        await reloadAsync();
    }

    private static string getProgressText(ApprovalRequestItem request)
    {
        if (request.Status == EApprovalRequestStatus.Publishing)
        {
            return "모든 결재가 끝났습니다. 승인본 저장이 완료되면 완료 기록으로 이동합니다.";
        }

        ApprovalStepItem? currentStepOrNull = request.Steps.FirstOrDefault(step => step.StepNumber == request.CurrentStepNumber);
        if (currentStepOrNull == null)
        {
            return "결재 진행 중 · 현재 담당자 정보를 확인해 주세요.";
        }

        return request.CurrentStepNumber + "/" + request.StepCount + "단계 · " +
            currentStepOrNull.ApproverDisplayName + " (" + currentStepOrNull.GetApproverAffiliationText() + ") 승인 대기";
    }

    private static string getStatusText(ApprovalRequestItem approvalRequest)
    {
        if (approvalRequest.Status == EApprovalRequestStatus.Pending)
        {
            return "결재 진행 중";
        }

        if (approvalRequest.Status == EApprovalRequestStatus.Publishing)
        {
            return "승인본 저장 중";
        }

        if (approvalRequest.Status == EApprovalRequestStatus.Approved)
        {
            return "완료";
        }

        return "반려";
    }

    private static string getStatusClass(ApprovalRequestItem approvalRequest)
    {
        if (approvalRequest.Status == EApprovalRequestStatus.Publishing || approvalRequest.Status == EApprovalRequestStatus.Pending)
        {
            return "hub-status-pill";
        }

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
