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
        summaries.Add(createDriveSummary("Drive", "기본 자료 위치", mFormModel.OfficialRootFolderID));
        summaries.Add(createDriveSummary("Drive", "공통 자료", mFormModel.CommonFolderID));
        summaries.Add(createDriveSummary("Drive", "외부 공유 자료", mFormModel.PublicFolderID));
        summaries.Add(createDriveSummary("Drive", "책임자 자료", mFormModel.ResponsibleResourcesFolderID));
        summaries.Add(createDriveSummary("Drive", "업무 지원 자료", mFormModel.WorkResourcesFolderID));
        summaries.Add(createDriveSummary("파일", "공지 파일", mFormModel.NoticeFileID));
        summaries.Add(createDriveSummary("파일", "업무 기준 파일", mFormModel.WorkGuidelineFileID));
        summaries.Add(createCalendarSummary("Calendar", "조직 공용 Calendar", mFormModel.OrganizationCalendarID));
        summaries.Add(createCalendarSummary("Calendar", "책임자 Calendar", mFormModel.ResponsibleCalendarID));
        summaries.Add(createTaskListSummary("Google Tasks", "조직 공용 Google Tasks", mFormModel.OrganizationTaskListID));
        summaries.Add(createTaskListSummary("Google Tasks", "책임자 Google Tasks", mFormModel.ResponsibleTaskListID));
        return summaries;
    }

    private SettingResourceSummary createDriveSummary(string groupName, string displayName, string resourceID)
    {
        SettingResourceSummary summary = new SettingResourceSummary();
        summary.GroupName = groupName;
        summary.DisplayName = displayName;
        summary.IsConfigured = string.IsNullOrWhiteSpace(resourceID) == false;
        summary.Detail = getDriveResourceName(resourceID);
        return summary;
    }

    private SettingResourceSummary createCalendarSummary(string groupName, string displayName, string calendarID)
    {
        SettingResourceSummary summary = new SettingResourceSummary();
        summary.GroupName = groupName;
        summary.DisplayName = displayName;
        summary.IsConfigured = string.IsNullOrWhiteSpace(calendarID) == false;
        summary.Detail = getCalendarName(calendarID);
        return summary;
    }

    private SettingResourceSummary createTaskListSummary(string groupName, string displayName, string taskListID)
    {
        SettingResourceSummary summary = new SettingResourceSummary();
        summary.GroupName = groupName;
        summary.DisplayName = displayName;
        summary.IsConfigured = string.IsNullOrWhiteSpace(taskListID) == false;
        summary.Detail = getTaskListName(taskListID);
        return summary;
    }

    private IEnumerable<string> getConfiguredDriveResourceIDs()
    {
        List<string> ids = new List<string>();
        addConfiguredID(ids, mFormModel.OfficialRootFolderID);
        addConfiguredID(ids, mFormModel.PublicFolderID);
        addConfiguredID(ids, mFormModel.CommonFolderID);
        addConfiguredID(ids, mFormModel.ResponsibleResourcesFolderID);
        addConfiguredID(ids, mFormModel.WorkResourcesFolderID);
        addConfiguredID(ids, mFormModel.NoticeFileID);
        addConfiguredID(ids, mFormModel.WorkGuidelineFileID);
        foreach (Team team in mTeams)
        {
            addConfiguredID(ids, getNullAsEmpty(team.MaterialsFolderID));
            addConfiguredID(ids, getNullAsEmpty(team.ApprovedFolderID));
        }

        return ids.Distinct();
    }

    private void addConfiguredID(List<string> ids, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ids.Add(value.Trim());
    }

    private string getDriveResourceName(string resourceID)
    {
        if (string.IsNullOrWhiteSpace(resourceID))
        {
            return "아직 연결되지 않았습니다.";
        }

        string trimmedResourceID = resourceID.Trim();
        string? nameOrNull;
        if (mDriveResourceNameByID.TryGetValue(trimmedResourceID, out nameOrNull))
        {
            return nameOrNull;
        }

        return "연결됨";
    }

    private string getCalendarName(string calendarID)
    {
        if (string.IsNullOrWhiteSpace(calendarID))
        {
            return "아직 연결되지 않았습니다.";
        }

        GoogleCalendarListItem? calendarOrNull = mCalendars.FirstOrDefault(calendar => calendar.ID == calendarID);
        if (calendarOrNull == null)
        {
            return "연결됨";
        }

        return calendarOrNull.Summary;
    }

    private string getTaskListName(string taskListID)
    {
        if (string.IsNullOrWhiteSpace(taskListID))
        {
            return "아직 연결되지 않았습니다.";
        }

        GoogleTaskListItem? taskListOrNull = mTaskLists.FirstOrDefault(taskList => taskList.ID == taskListID);
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

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }

    private sealed class SettingResourceSummary
    {
        public string GroupName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public bool IsConfigured
        {
            get; set;
        }
    }
}
