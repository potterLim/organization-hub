using System;
using System.Globalization;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
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

    private string getCurrentStepRoleName(ApprovalRequestItem approvalRequest)
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        return ApprovalStepLabelUtility.GetStepRoleName(stepCount, new ApprovalStepNumber(approvalRequest.CurrentStepNumber));
    }

    private string getStepRoleName(ApprovalRequestItem approvalRequest, ApprovalStepItem step)
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        return ApprovalStepLabelUtility.GetStepRoleName(stepCount, new ApprovalStepNumber(step.StepNumber));
    }

    private string formatDate(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
