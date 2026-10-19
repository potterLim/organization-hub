using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private IReadOnlyList<SettingResourceSummary> getPrimaryResourceSummaries()
    {
        IReadOnlyList<SettingResourceSummaryRequest> requests = new List<SettingResourceSummaryRequest>
        {
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Drive,
                GroupName = "Drive",
                DisplayName = "기본 자료 위치",
                ResourceId = mFormModel.DriveResources.OfficialRootFolderId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Drive,
                GroupName = "Drive",
                DisplayName = "공통 자료",
                ResourceId = mFormModel.DriveResources.CommonFolderId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Drive,
                GroupName = "Drive",
                DisplayName = "외부 공유 자료",
                ResourceId = mFormModel.DriveResources.PublicFolderId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Drive,
                GroupName = "Drive",
                DisplayName = "책임자 자료",
                ResourceId = mFormModel.DriveResources.ResponsibleResourcesFolderId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Drive,
                GroupName = "Drive",
                DisplayName = "업무 지원 자료",
                ResourceId = mFormModel.DriveResources.WorkResourcesFolderId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Drive,
                GroupName = "파일",
                DisplayName = "공지 파일",
                ResourceId = mFormModel.FileResources.NoticeFileId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Drive,
                GroupName = "파일",
                DisplayName = "업무 기준 파일",
                ResourceId = mFormModel.FileResources.WorkGuidelineFileId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Calendar,
                GroupName = "Calendar",
                DisplayName = "조직 공용 Calendar",
                ResourceId = mFormModel.CalendarResources.OrganizationCalendarId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.Calendar,
                GroupName = "Calendar",
                DisplayName = "책임자 Calendar",
                ResourceId = mFormModel.CalendarResources.ResponsibleCalendarId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.TaskList,
                GroupName = "Google Tasks",
                DisplayName = "조직 공용 Google Tasks",
                ResourceId = mFormModel.TaskResources.OrganizationTaskListId
            },
            new SettingResourceSummaryRequest
            {
                ResourceKind = ESettingResourceSummaryKind.TaskList,
                GroupName = "Google Tasks",
                DisplayName = "책임자 Google Tasks",
                ResourceId = mFormModel.TaskResources.ResponsibleTaskListId
            }
        };

        return requests.Select(createResourceSummary).ToList();
    }

    private SettingResourceSummary createResourceSummary(SettingResourceSummaryRequest request)
    {
        SettingResourceSummary summary = new SettingResourceSummary();
        summary.GroupName = request.GroupName;
        summary.DisplayName = request.DisplayName;
        summary.IsConfigured = string.IsNullOrWhiteSpace(request.ResourceId) == false;
        summary.Detail = getResourceSummaryDetail(request);
        return summary;
    }

    private string getResourceSummaryDetail(SettingResourceSummaryRequest request)
    {
        switch (request.ResourceKind)
        {
            case ESettingResourceSummaryKind.Drive:
                return getDriveResourceName(request.ResourceId);
            case ESettingResourceSummaryKind.Calendar:
                return getCalendarName(request.ResourceId);
            case ESettingResourceSummaryKind.TaskList:
                return getTaskListName(request.ResourceId);
            default:
                return "위치 지정됨";
        }
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

        return "위치 지정됨";
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
            return "위치 지정됨";
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
            return "위치 지정됨";
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
