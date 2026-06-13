using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private const string NOTICE_FILE_TARGET_KEY = "Notice";
    private const string WORK_GUIDELINE_FILE_TARGET_KEY = "WorkGuideline";
    private const string DRIVE_ROOT_ID = "root";

    private GlobalSettingFormModel mFormModel = new GlobalSettingFormModel();
    private GoogleConnectionStatus mStatus = new GoogleConnectionStatus();
    private List<Team> mTeams = new List<Team>();
    private List<GoogleFolderAssignmentOption> mFolderAssignmentOptions = new List<GoogleFolderAssignmentOption>();
    private List<GoogleCalendarAssignmentOption> mCalendarAssignmentOptions = new List<GoogleCalendarAssignmentOption>();
    private List<GoogleTaskListAssignmentOption> mTaskListAssignmentOptions = new List<GoogleTaskListAssignmentOption>();
    private List<GoogleDriveFolderPathItem> mFolderPath = new List<GoogleDriveFolderPathItem>();
    private List<DriveFileItem> mDriveItems = new List<DriveFileItem>();
    private List<GoogleCalendarListItem> mCalendars = new List<GoogleCalendarListItem>();
    private List<GoogleTaskListItem> mTaskLists = new List<GoogleTaskListItem>();
    private Dictionary<string, string> mDriveResourceNameByID = new Dictionary<string, string>();
    private string mSelectedFolderTargetKey = string.Empty;
    private string mSelectedCalendarTargetKey = string.Empty;
    private string mSelectedTaskListTargetKey = string.Empty;
    private string mSelectedFileTargetKey = NOTICE_FILE_TARGET_KEY;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mIsLoadingDriveItems;
    private bool mIsRefreshing;

    protected override async Task OnInitializedAsync()
    {
        await loadConfigurationAsync();
        await loadConnectionStatusAsync();
        await loadAssignmentOptionsAsync();
        resetDrivePathToOfficialRoot();
        if (mStatus.IsConnected)
        {
            Task driveItemsTask = loadDriveItemsAsync();
            Task calendarsTask = loadCalendarsAsync();
            Task taskListsTask = loadTaskListsAsync();
            Task driveResourceNamesTask = loadDriveResourceNamesAsync();
            await Task.WhenAll(driveItemsTask, calendarsTask, taskListsTask, driveResourceNamesTask);
        }
    }

    private async Task loadConfigurationAsync()
    {
        mFormModel = await TeamManagementService.GetGlobalSettingAsync(CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        mTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
    }

    private async Task loadConnectionStatusAsync()
    {
        mStatus = await GoogleConnectionService.GetStatusAsync(CancellationToken.None);
    }

    private async Task loadAssignmentOptionsAsync()
    {
        IReadOnlyList<GoogleFolderAssignmentOption> folderOptions = await GoogleWorkspaceSetupService.GetFolderAssignmentOptionsAsync(CancellationToken.None);
        mFolderAssignmentOptions = folderOptions.ToList();
        if (string.IsNullOrWhiteSpace(mSelectedFolderTargetKey) && mFolderAssignmentOptions.Count > 0)
        {
            mSelectedFolderTargetKey = mFolderAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleCalendarAssignmentOption> calendarOptions = await GoogleWorkspaceSetupService.GetCalendarAssignmentOptionsAsync(CancellationToken.None);
        mCalendarAssignmentOptions = calendarOptions.ToList();
        if (string.IsNullOrWhiteSpace(mSelectedCalendarTargetKey) && mCalendarAssignmentOptions.Count > 0)
        {
            mSelectedCalendarTargetKey = mCalendarAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleTaskListAssignmentOption> taskListOptions = await GoogleWorkspaceSetupService.GetTaskListAssignmentOptionsAsync(CancellationToken.None);
        mTaskListAssignmentOptions = taskListOptions.ToList();
        if (string.IsNullOrWhiteSpace(mSelectedTaskListTargetKey) && mTaskListAssignmentOptions.Count > 0)
        {
            mSelectedTaskListTargetKey = mTaskListAssignmentOptions[0].TargetKey;
        }
    }

    private async Task refreshAllAsync()
    {
        mIsRefreshing = true;
        try
        {
            await loadConfigurationAsync();
            await loadConnectionStatusAsync();
            await loadAssignmentOptionsAsync();
            if (mStatus.IsConnected)
            {
                Task driveItemsTask = loadDriveItemsAsync();
                Task calendarsTask = loadCalendarsAsync();
                Task taskListsTask = loadTaskListsAsync();
                Task driveResourceNamesTask = loadDriveResourceNamesAsync();
                await Task.WhenAll(driveItemsTask, calendarsTask, taskListsTask, driveResourceNamesTask);
            }

            mMessage = "설정을 다시 불러왔습니다.";
            mMessageSeverity = Severity.Success;
        }
        finally
        {
            mIsRefreshing = false;
        }
    }

    private async Task loadDriveItemsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        mIsLoadingDriveItems = true;
        try
        {
            IReadOnlyList<DriveFileItem> driveItems = await DriveService.ListFilesAsync(getCurrentFolderID(), CancellationToken.None);
            mDriveItems = driveItems.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Drive 목록을 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsLoadingDriveItems = false;
        }
    }

    private async Task loadCalendarsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        try
        {
            IReadOnlyList<GoogleCalendarListItem> calendars = await GoogleWorkspaceBrowserService.ListCalendarsAsync(CancellationToken.None);
            mCalendars = calendars.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Calendar 목록을 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task loadTaskListsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        try
        {
            IReadOnlyList<GoogleTaskListItem> taskLists = await GoogleWorkspaceBrowserService.ListTaskListsAsync(CancellationToken.None);
            mTaskLists = taskLists.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Google Tasks 목록을 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task loadDriveResourceNamesAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        Dictionary<string, string> namesByID = new Dictionary<string, string>();
        foreach (string driveResourceID in getConfiguredDriveResourceIDs())
        {
            if (namesByID.ContainsKey(driveResourceID))
            {
                continue;
            }

            try
            {
                DriveFileMetadata? metadataOrNull = await DriveService.GetFileOrNullAsync(driveResourceID, CancellationToken.None);
                if (metadataOrNull == null)
                {
                    namesByID[driveResourceID] = "연결된 항목을 찾을 수 없음";
                    continue;
                }

                namesByID[driveResourceID] = metadataOrNull.Name;
            }
            catch
            {
                namesByID[driveResourceID] = "이름을 확인하지 못함";
            }
        }

        mDriveResourceNameByID = namesByID;
    }

    private async Task openFolderAsync(DriveFileItem folder)
    {
        if (folder.IsFolder == false)
        {
            return;
        }

        GoogleDriveFolderPathItem pathItem = new GoogleDriveFolderPathItem();
        pathItem.ID = folder.ID;
        pathItem.Name = folder.Name;
        mFolderPath.Add(pathItem);
        await loadDriveItemsAsync();
    }

    private async Task openPathAsync(int index)
    {
        if (index < 0 || index >= mFolderPath.Count)
        {
            return;
        }

        mFolderPath = mFolderPath.Take(index + 1).ToList();
        await loadDriveItemsAsync();
    }

    private async Task goToDriveRootAsync()
    {
        resetDrivePathToRoot();
        await loadDriveItemsAsync();
    }

    private async Task goToOfficialRootAsync()
    {
        resetDrivePathToOfficialRoot();
        await loadDriveItemsAsync();
    }

    private async Task assignFolderAsync(DriveFileItem folder)
    {
        if (folder.IsFolder == false)
        {
            return;
        }

        ServiceResult result = await GoogleWorkspaceSetupService.AssignFolderAsync(mSelectedFolderTargetKey, folder.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? folder.Name + " 폴더를 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private async Task assignFileAsync(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return;
        }

        if (mSelectedFileTargetKey == NOTICE_FILE_TARGET_KEY)
        {
            mFormModel.NoticeFileID = file.ID;
        }
        else
        {
            mFormModel.WorkGuidelineFileID = file.ID;
        }

        ServiceResult result = await TeamManagementService.UpdateGlobalSettingAsync(mFormModel, CancellationToken.None);
        showResult(result, result.IsSuccess ? file.Name + " 파일을 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private async Task assignCalendarAsync(GoogleCalendarListItem calendar)
    {
        ServiceResult result = await GoogleWorkspaceSetupService.AssignCalendarAsync(mSelectedCalendarTargetKey, calendar.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? calendar.Summary + " Calendar를 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
        }
    }

    private async Task assignTaskListAsync(GoogleTaskListItem taskList)
    {
        ServiceResult result = await GoogleWorkspaceSetupService.AssignTaskListAsync(mSelectedTaskListTargetKey, taskList.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? taskList.Title + " Google Tasks 목록을 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
        }
    }

    private async Task saveSettingAsync()
    {
        ServiceResult result = await TeamManagementService.UpdateGlobalSettingAsync(mFormModel, CancellationToken.None);
        showResult(result, "시스템 설정이 저장되었습니다.");
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadDriveResourceNamesAsync();
        }
    }

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

    private void setFileTarget(string targetKey)
    {
        mSelectedFileTargetKey = targetKey;
    }

    private void resetDrivePathToRoot()
    {
        GoogleDriveFolderPathItem rootPathItem = new GoogleDriveFolderPathItem();
        rootPathItem.ID = DRIVE_ROOT_ID;
        rootPathItem.Name = "내 Drive";
        mFolderPath = new List<GoogleDriveFolderPathItem>();
        mFolderPath.Add(rootPathItem);
    }

    private void resetDrivePathToOfficialRoot()
    {
        if (string.IsNullOrWhiteSpace(mFormModel.OfficialRootFolderID))
        {
            resetDrivePathToRoot();
            return;
        }

        GoogleDriveFolderPathItem rootPathItem = new GoogleDriveFolderPathItem();
        rootPathItem.ID = mFormModel.OfficialRootFolderID;
        rootPathItem.Name = "기본 자료";
        mFolderPath = new List<GoogleDriveFolderPathItem>();
        mFolderPath.Add(rootPathItem);
    }

    private List<DriveFileItem> getOrderedDriveItems()
    {
        return mDriveItems.OrderBy(item => item.IsFolder ? 0 : 1).ThenBy(item => item.IsFolder ? item.Name : string.Empty).ThenByDescending(item => item.ModifiedTime).ThenBy(item => item.Name).ToList();
    }

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

    private string getSelectedFolderAssignmentDisplayName()
    {
        GoogleFolderAssignmentOption? optionOrNull = getSelectedFolderAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedFolderAssignmentDetail()
    {
        GoogleFolderAssignmentOption? optionOrNull = getSelectedFolderAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getDriveResourceName(getFolderAssignmentID(optionOrNull));
    }

    private string getSelectedFileAssignmentDisplayName()
    {
        if (mSelectedFileTargetKey == NOTICE_FILE_TARGET_KEY)
        {
            return "공지 파일";
        }

        return "업무 기준 파일";
    }

    private string getSelectedFileAssignmentDetail()
    {
        if (mSelectedFileTargetKey == NOTICE_FILE_TARGET_KEY)
        {
            return getDriveResourceName(mFormModel.NoticeFileID);
        }

        return getDriveResourceName(mFormModel.WorkGuidelineFileID);
    }

    private string getSelectedCalendarAssignmentDisplayName()
    {
        GoogleCalendarAssignmentOption? optionOrNull = getSelectedCalendarAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedCalendarAssignmentDetail()
    {
        GoogleCalendarAssignmentOption? optionOrNull = getSelectedCalendarAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getCalendarName(getCalendarAssignmentID(optionOrNull));
    }

    private string getSelectedTaskListAssignmentDisplayName()
    {
        GoogleTaskListAssignmentOption? optionOrNull = getSelectedTaskListAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedTaskListAssignmentDetail()
    {
        GoogleTaskListAssignmentOption? optionOrNull = getSelectedTaskListAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getTaskListName(getTaskListAssignmentID(optionOrNull));
    }

    private GoogleFolderAssignmentOption? getSelectedFolderAssignmentOptionOrNull()
    {
        return mFolderAssignmentOptions.FirstOrDefault(option => option.TargetKey == mSelectedFolderTargetKey);
    }

    private GoogleCalendarAssignmentOption? getSelectedCalendarAssignmentOptionOrNull()
    {
        return mCalendarAssignmentOptions.FirstOrDefault(option => option.TargetKey == mSelectedCalendarTargetKey);
    }

    private GoogleTaskListAssignmentOption? getSelectedTaskListAssignmentOptionOrNull()
    {
        return mTaskListAssignmentOptions.FirstOrDefault(option => option.TargetKey == mSelectedTaskListTargetKey);
    }

    private string getFolderAssignmentID(GoogleFolderAssignmentOption option)
    {
        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.OfficialRoot)
        {
            return mFormModel.OfficialRootFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.Public)
        {
            return mFormModel.PublicFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.Common)
        {
            return mFormModel.CommonFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.ResponsibleResources)
        {
            return mFormModel.ResponsibleResourcesFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.WorkResources)
        {
            return mFormModel.WorkResourcesFolderID;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamID);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.TeamMaterials)
        {
            return getNullAsEmpty(teamOrNull.MaterialsFolderID);
        }

        return getNullAsEmpty(teamOrNull.ApprovedFolderID);
    }

    private string getCalendarAssignmentID(GoogleCalendarAssignmentOption option)
    {
        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Organization)
        {
            return mFormModel.OrganizationCalendarID;
        }

        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Leader)
        {
            return mFormModel.ResponsibleCalendarID;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamID);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        return getNullAsEmpty(teamOrNull.CalendarID);
    }

    private string getTaskListAssignmentID(GoogleTaskListAssignmentOption option)
    {
        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Organization)
        {
            return mFormModel.OrganizationTaskListID;
        }

        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Leader)
        {
            return mFormModel.ResponsibleTaskListID;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamID);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        return getNullAsEmpty(teamOrNull.TaskListID);
    }

    private Team? getTeamOrNull(int? teamIDOrNull)
    {
        if (teamIDOrNull.HasValue == false)
        {
            return null;
        }

        return mTeams.FirstOrDefault(team => team.ID == teamIDOrNull.Value);
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

    private string getFileTypeDisplayName(DriveFileItem file)
    {
        if (string.IsNullOrWhiteSpace(file.MimeType))
        {
            return "파일";
        }

        if (file.MimeType == "application/pdf")
        {
            return "PDF";
        }

        if (file.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "이미지";
        }

        return "파일";
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

    private string getCurrentFolderID()
    {
        if (mFolderPath.Count == 0)
        {
            return DRIVE_ROOT_ID;
        }

        return mFolderPath[mFolderPath.Count - 1].ID;
    }

    private string formatDate(DateTime? dateOrNull)
    {
        if (dateOrNull.HasValue == false)
        {
            return "-";
        }

        return dateOrNull.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
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
