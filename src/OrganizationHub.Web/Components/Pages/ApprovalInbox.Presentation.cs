using System;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
        if (operationResult.IsPending)
        {
            mMessage = string.Empty;
            mMessageSeverity = Severity.Info;
            return;
        }
        if (operationResult.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = operationResult.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }

    private string getStepClass(ApprovalStepItem step)
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

    private string getCurrentStepLabel(ApprovalRequestItem approvalRequest)
    {
        if (approvalRequest.Status == EApprovalRequestStatus.Publishing)
        {
            return "승인본 저장 중";
        }

        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        return ApprovalStepLabelUtility.GetStepLabel(stepCount, new ApprovalStepNumber(approvalRequest.CurrentStepNumber));
    }

    private string getStepLabel(ApprovalRequestItem approvalRequest, ApprovalStepItem step)
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        return ApprovalStepLabelUtility.GetStepLabel(stepCount, new ApprovalStepNumber(step.StepNumber));
    }

    private string getStepDisplayText(ApprovalRequestItem approvalRequest, ApprovalStepItem step)
    {
        return getStepLabel(approvalRequest, step) + ": " + step.GetApproverAffiliationText();
    }

    private bool shouldShowNextStepSummary(ApprovalRequestItem approvalRequest)
    {
        return getNextStepOrNull(approvalRequest) != null;
    }

    private string getNextStepSummaryLabel(ApprovalRequestItem approvalRequest)
    {
        ApprovalStepItem? nextStepOrNull = getNextStepOrNull(approvalRequest);
        if (nextStepOrNull == null)
        {
            return string.Empty;
        }

        string stepLabel = getStepLabel(approvalRequest, nextStepOrNull);
        if (stepLabel == "결재권자")
        {
            return "결재권자";
        }

        return "다음 검토자";
    }

    private string getNextStepSummaryText(ApprovalRequestItem approvalRequest)
    {
        ApprovalStepItem? nextStepOrNull = getNextStepOrNull(approvalRequest);
        if (nextStepOrNull == null)
        {
            return string.Empty;
        }

        return getNextStepSummaryLabel(approvalRequest) + ": " + nextStepOrNull.GetApproverAffiliationText();
    }

    private bool hasApprovedFileConflict(ApprovalRequestItem approvalRequest)
    {
        return mApprovedFileConflictsByApprovalId.ContainsKey(approvalRequest.Id);
    }

    private string getApprovedFileConflictTitle(ApprovalRequestItem approvalRequest)
    {
        DriveFileNameConflictCheckResult? conflictCheckResultOrNull = getApprovedFileConflictOrNull(approvalRequest);
        if (conflictCheckResultOrNull == null)
        {
            return string.Empty;
        }

        return "승인본 저장 위치에 같은 이름의 파일이 " + conflictCheckResultOrNull.ExistingFiles.Count.ToString(CultureInfo.InvariantCulture) + "개 있습니다.";
    }

    private string getApprovedFileSuggestedFileName(ApprovalRequestItem approvalRequest)
    {
        DriveFileNameConflictCheckResult? conflictCheckResultOrNull = getApprovedFileConflictOrNull(approvalRequest);
        if (conflictCheckResultOrNull == null)
        {
            return string.Empty;
        }

        return conflictCheckResultOrNull.SuggestedNewFileName.GetRequiredValue();
    }

    private DriveFileNameConflictCheckResult? getApprovedFileConflictOrNull(ApprovalRequestItem approvalRequest)
    {
        DriveFileNameConflictCheckResult? conflictCheckResultOrNull;
        if (mApprovedFileConflictsByApprovalId.TryGetValue(approvalRequest.Id, out conflictCheckResultOrNull))
        {
            return conflictCheckResultOrNull;
        }

        return null;
    }

    private static bool isFinalApprovalStep(ApprovalRequestItem approvalRequest)
    {
        return approvalRequest.CurrentStepNumber == approvalRequest.StepCount;
    }

    private ApprovalStepItem? getNextStepOrNull(ApprovalRequestItem approvalRequest)
    {
        int nextStepNumber = approvalRequest.CurrentStepNumber + 1;
        if (nextStepNumber > approvalRequest.StepCount)
        {
            return null;
        }

        return approvalRequest.Steps.FirstOrDefault(step => step.StepNumber == nextStepNumber);
    }

    private string formatDate(DateTime dateTime)
    {
        return PresentationDateTimeFormatter.formatClientDateTime(dateTime, ClientTimeZoneInfo, PresentationDateTimeFormatter.FULL_DATE_TIME_FORMAT);
    }
}
