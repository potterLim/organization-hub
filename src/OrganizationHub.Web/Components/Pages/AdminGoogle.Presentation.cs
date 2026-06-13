using System;
using System.Globalization;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    private void setFolderTarget(string targetKey)
    {
        mSelectedFolderTargetKey = targetKey;
    }

    private void setCalendarTarget(string targetKey)
    {
        mSelectedCalendarTargetKey = targetKey;
    }

    private void setTaskListTarget(string targetKey)
    {
        mSelectedTaskListTargetKey = targetKey;
    }

    private string createSetupSummaryMessage(string prefix, GoogleWorkspaceResourceSetupSummary summary)
    {
        return prefix + ": 생성 " + summary.CreatedCount.ToString(CultureInfo.InvariantCulture) + "개, 재사용 " + summary.ReusedCount.ToString(CultureInfo.InvariantCulture) + "개, 매핑 " + summary.AssignedCount.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private void showResult(ServiceResult result, string successMessage)
    {
        if (result.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = result.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }

    private string formatDate(DateTime? dateTimeOrNull)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return dateTimeOrNull.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string getCallbackUri()
    {
        return NavigationManager.ToAbsoluteUri("/admin/google/callback").ToString();
    }
}
