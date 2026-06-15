using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Dashboard
{
    private bool shouldShowPendingApprovalsCard()
    {
        return mIsLoadingPendingApprovals || string.IsNullOrWhiteSpace(mPendingApprovalsErrorMessage) == false || mPendingApprovals.Count > 0;
    }

    private string formatEventTime(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.Start.HasValue == false)
        {
            return "시간 미정";
        }

        if (calendarEvent.IsAllDay)
        {
            return calendarEvent.Start.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 종일";
        }

        string startText = calendarEvent.Start.Value.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (calendarEvent.End.HasValue == false)
        {
            return startText;
        }

        return startText + " - " + calendarEvent.End.Value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private string formatDashboardEventMeta(CalendarEventItem calendarEvent)
    {
        List<string> metadataParts = new List<string>();
        if (shouldShowTeamNames())
        {
            metadataParts.Add(calendarEvent.CalendarName);
        }

        if (shouldShowCalendarAuthor(calendarEvent))
        {
            metadataParts.Add("작성자: " + calendarEvent.CreatedByDisplayName);
        }

        metadataParts.Add(formatEventTime(calendarEvent));
        return string.Join(" · ", metadataParts);
    }

    private bool shouldShowTeamNames()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return false;
        }

        return CurrentUserSessionOrNull.IsManagementRole();
    }

    private bool shouldShowCalendarAuthor(CalendarEventItem calendarEvent)
    {
        return mShowCalendarAuthor && string.IsNullOrWhiteSpace(calendarEvent.CreatedByDisplayName) == false;
    }

    private IReadOnlyList<DriveFileItem> getMobileRecentMaterials()
    {
        return mRecentMaterials.Take(3).ToList();
    }

    private string getRecentMaterialPath(DriveFileItem file)
    {
        string folderName = file.FolderDisplayName.Trim();
        if (string.IsNullOrWhiteSpace(folderName) == false)
        {
            return folderName;
        }

        return getRecentMaterialTeamName(file);
    }

    private string getRecentMaterialUpdatedText(DriveFileItem file)
    {
        if (file.UploadedAt.HasValue)
        {
            return "업로드: " + file.UploadedAt.Value.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        if (file.ModifiedTime.HasValue == false)
        {
            return "수정일 없음";
        }

        return "수정일: " + file.ModifiedTime.Value.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string getRecentMaterialUploaderText(DriveFileItem file)
    {
        return "올린 사람: " + file.UploadedByDisplayName.Trim();
    }

    private string getRecentMaterialTeamName(DriveFileItem file)
    {
        if (string.IsNullOrWhiteSpace(file.TeamName))
        {
            return "분류 없음";
        }

        return file.TeamName.Trim();
    }

    private string getFileIcon(DriveFileItem file)
    {
        if (file.MimeType == "application/pdf")
        {
            return Icons.Material.Filled.PictureAsPdf;
        }

        return Icons.Material.Filled.InsertDriveFile;
    }

    private string getCurrentApprovalStepRoleName(ApprovalRequestItem approvalRequest)
    {
        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        return ApprovalStepLabelUtility.GetStepRoleName(stepCount, new ApprovalStepNumber(approvalRequest.CurrentStepNumber));
    }

    private string formatDate(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
