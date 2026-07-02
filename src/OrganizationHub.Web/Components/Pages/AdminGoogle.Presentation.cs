using System;
using System.Globalization;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    private void setFolderTarget(GoogleWorkspaceAssignmentTargetKey targetKey)
    {
        mSelectedFolderTargetKey = targetKey;
    }

    private void setCalendarTarget(GoogleWorkspaceAssignmentTargetKey targetKey)
    {
        mSelectedCalendarTargetKey = targetKey;
    }

    private void setTaskListTarget(GoogleWorkspaceAssignmentTargetKey targetKey)
    {
        mSelectedTaskListTargetKey = targetKey;
    }

    private string createSetupSummaryMessage(string prefix, GoogleWorkspaceResourceSetupSummary summary)
    {
        return prefix + ": 생성 " + summary.CreatedCount.ToString(CultureInfo.InvariantCulture) + "개, 재사용 " + summary.ReusedCount.ToString(CultureInfo.InvariantCulture) + "개, 매핑 " + summary.AssignedCount.ToString(CultureInfo.InvariantCulture) + "개";
    }

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

    private string formatDate(DateTime? dateTimeOrNull)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return PresentationDateTimeFormatter.formatClientDateTime(dateTimeOrNull.Value, ClientTimeZoneInfo, PresentationDateTimeFormatter.FULL_DATE_TIME_FORMAT);
    }

    private string getCallbackUri()
    {
        return NavigationManager.ToAbsoluteUri("/admin/google/callback").ToString();
    }
}
