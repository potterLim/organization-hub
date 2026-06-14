using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private IReadOnlyList<SettingResourceSummary> getPrimaryResourceSummaries()
    {
        List<SettingResourceSummary> summaries = new List<SettingResourceSummary>();
        summaries.Add(createDriveSummary("Drive", "기본 자료 위치", mFormModel.DriveResources.OfficialRootFolderId));
        summaries.Add(createDriveSummary("Drive", "공통 자료", mFormModel.DriveResources.CommonFolderId));
        summaries.Add(createDriveSummary("Drive", "외부 공유 자료", mFormModel.DriveResources.PublicFolderId));
        summaries.Add(createDriveSummary("Drive", "책임자 자료", mFormModel.DriveResources.ResponsibleResourcesFolderId));
        summaries.Add(createDriveSummary("Drive", "업무 지원 자료", mFormModel.DriveResources.WorkResourcesFolderId));
        summaries.Add(createDriveSummary("파일", "공지 파일", mFormModel.FileResources.NoticeFileId));
        summaries.Add(createDriveSummary("파일", "업무 기준 파일", mFormModel.FileResources.WorkGuidelineFileId));
        summaries.Add(createCalendarSummary("Calendar", "조직 공용 Calendar", mFormModel.CalendarResources.OrganizationCalendarId));
        summaries.Add(createCalendarSummary("Calendar", "책임자 Calendar", mFormModel.CalendarResources.ResponsibleCalendarId));
        summaries.Add(createTaskListSummary("Google Tasks", "조직 공용 Google Tasks", mFormModel.TaskResources.OrganizationTaskListId));
        summaries.Add(createTaskListSummary("Google Tasks", "책임자 Google Tasks", mFormModel.TaskResources.ResponsibleTaskListId));
        return summaries;
    }

    private SettingResourceSummary createDriveSummary(string groupName, string displayName, string resourceId)
    {
        SettingResourceSummary summary = new SettingResourceSummary();
        summary.GroupName = groupName;
        summary.DisplayName = displayName;
        summary.IsConfigured = string.IsNullOrWhiteSpace(resourceId) == false;
        summary.Detail = getDriveResourceName(resourceId);
        return summary;
    }

    private SettingResourceSummary createCalendarSummary(string groupName, string displayName, string calendarId)
    {
        SettingResourceSummary summary = new SettingResourceSummary();
        summary.GroupName = groupName;
        summary.DisplayName = displayName;
        summary.IsConfigured = string.IsNullOrWhiteSpace(calendarId) == false;
        summary.Detail = getCalendarName(calendarId);
        return summary;
    }

    private SettingResourceSummary createTaskListSummary(string groupName, string displayName, string taskListId)
    {
        SettingResourceSummary summary = new SettingResourceSummary();
        summary.GroupName = groupName;
        summary.DisplayName = displayName;
        summary.IsConfigured = string.IsNullOrWhiteSpace(taskListId) == false;
        summary.Detail = getTaskListName(taskListId);
        return summary;
    }

    private IEnumerable<string> getConfiguredDriveResourceIds()
    {
        List<string> configuredDriveResourceIds = new List<string>();
        addConfiguredId(configuredDriveResourceIds, mFormModel.DriveResources.OfficialRootFolderId);
        addConfiguredId(configuredDriveResourceIds, mFormModel.DriveResources.PublicFolderId);
        addConfiguredId(configuredDriveResourceIds, mFormModel.DriveResources.CommonFolderId);
        addConfiguredId(configuredDriveResourceIds, mFormModel.DriveResources.ResponsibleResourcesFolderId);
        addConfiguredId(configuredDriveResourceIds, mFormModel.DriveResources.WorkResourcesFolderId);
        addConfiguredId(configuredDriveResourceIds, mFormModel.FileResources.NoticeFileId);
        addConfiguredId(configuredDriveResourceIds, mFormModel.FileResources.WorkGuidelineFileId);
        foreach (Team team in mTeams)
        {
            addConfiguredId(configuredDriveResourceIds, getTextOrEmpty(team.MaterialsFolderId));
            addConfiguredId(configuredDriveResourceIds, getTextOrEmpty(team.ApprovedFolderId));
        }

        return configuredDriveResourceIds.Distinct();
    }

    private void addConfiguredId(List<string> configuredIds, string configuredId)
    {
        if (string.IsNullOrWhiteSpace(configuredId))
        {
            return;
        }

        configuredIds.Add(configuredId.Trim());
    }

    private string getDriveResourceName(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return "아직 연결되지 않았습니다.";
        }

        string trimmedResourceId = resourceId.Trim();
        string? nameOrNull;
        if (mDriveResourceNameById.TryGetValue(trimmedResourceId, out nameOrNull))
        {
            return nameOrNull;
        }

        return "연결됨";
    }

    private string getCalendarName(string calendarId)
    {
        if (string.IsNullOrWhiteSpace(calendarId))
        {
            return "아직 연결되지 않았습니다.";
        }

        GoogleCalendarListItem? calendarOrNull = mCalendars.FirstOrDefault(calendar => calendar.Id == calendarId);
        if (calendarOrNull == null)
        {
            return "연결됨";
        }

        return calendarOrNull.Summary;
    }

    private string getTaskListName(string taskListId)
    {
        if (string.IsNullOrWhiteSpace(taskListId))
        {
            return "아직 연결되지 않았습니다.";
        }

        GoogleTaskListItem? taskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.Id == taskListId);
        if (taskListOrNull == null)
        {
            return "연결됨";
        }

        return taskListOrNull.Title;
    }

    private string getCalendarAccessDisplayName(GoogleCalendarListItem calendar)
    {
        if (calendar.IsPrimary)
        {
            return "기본 Calendar";
        }

        if (string.IsNullOrWhiteSpace(calendar.AccessRole))
        {
            return "접근 가능";
        }

        switch (calendar.AccessRole)
        {
            case "owner":
                return "소유자";
            case "writer":
                return "편집 가능";
            case "reader":
                return "읽기 가능";
            default:
                return "접근 가능";
        }
    }

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }

}
