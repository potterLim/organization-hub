using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    [SupplyParameterFromQuery(Name = "message")]
    public string? MessageOrNull
    {
        get; set;
    }

    private GoogleConnectionStatus mStatus = new GoogleConnectionStatus();
    private List<GoogleDriveFolderItem> mFolders = new List<GoogleDriveFolderItem>();
    private List<GoogleCalendarListItem> mCalendars = new List<GoogleCalendarListItem>();
    private List<GoogleTaskListItem> mTaskLists = new List<GoogleTaskListItem>();
    private List<GoogleFolderAssignmentOption> mFolderAssignmentOptions = new List<GoogleFolderAssignmentOption>();
    private List<GoogleCalendarAssignmentOption> mCalendarAssignmentOptions = new List<GoogleCalendarAssignmentOption>();
    private List<GoogleTaskListAssignmentOption> mTaskListAssignmentOptions = new List<GoogleTaskListAssignmentOption>();
    private List<GoogleDriveFolderPathItem> mFolderPath = new List<GoogleDriveFolderPathItem>();
    private List<GoogleHealthCheckItem> mHealthItems = new List<GoogleHealthCheckItem>();
    private string mSelectedFolderTargetKey = string.Empty;
    private string mSelectedCalendarTargetKey = string.Empty;
    private string mSelectedTaskListTargetKey = string.Empty;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mShowHealthyHealthItems;
    private bool mIsEnsuringStandardDriveFolders;
    private bool mIsEnsuringStandardCalendars;
    private bool mIsApplyingStandardDriveFolderNames;
    private bool mIsEnsuringStandardTaskLists;

    protected override async Task OnInitializedAsync()
    {
        resetFolderPath();
        await loadStatusAsync();
        await loadAssignmentOptionsAsync();
        if (mStatus.IsConnected)
        {
            Task healthCheckTask = loadHealthCheckAsync();
            Task currentFolderTask = loadCurrentFolderAsync();
            Task calendarsTask = loadCalendarsAsync();
            Task taskListsTask = loadTaskListsAsync();
            await Task.WhenAll(healthCheckTask, currentFolderTask, calendarsTask, taskListsTask);
            return;
        }

        await loadHealthCheckAsync();
    }

    private async Task loadStatusAsync()
    {
        mStatus = await GoogleConnectionService.GetStatusAsync(CancellationToken.None);
    }

    private async Task loadAssignmentOptionsAsync()
    {
        IReadOnlyList<GoogleFolderAssignmentOption> folderOptions = await GoogleWorkspaceSetupService.GetFolderAssignmentOptionsAsync(CancellationToken.None);
        mFolderAssignmentOptions = folderOptions.ToList();
        if (mFolderAssignmentOptions.Count > 0)
        {
            mSelectedFolderTargetKey = mFolderAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleCalendarAssignmentOption> calendarOptions = await GoogleWorkspaceSetupService.GetCalendarAssignmentOptionsAsync(CancellationToken.None);
        mCalendarAssignmentOptions = calendarOptions.ToList();
        if (mCalendarAssignmentOptions.Count > 0)
        {
            mSelectedCalendarTargetKey = mCalendarAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleTaskListAssignmentOption> taskListOptions = await GoogleWorkspaceSetupService.GetTaskListAssignmentOptionsAsync(CancellationToken.None);
        mTaskListAssignmentOptions = taskListOptions.ToList();
        if (mTaskListAssignmentOptions.Count > 0)
        {
            mSelectedTaskListTargetKey = mTaskListAssignmentOptions[0].TargetKey;
        }
    }

    private async Task loadCurrentFolderAsync()
    {
        try
        {
            GoogleDriveFolderPathItem currentFolder = mFolderPath[mFolderPath.Count - 1];
            IReadOnlyList<GoogleDriveFolderItem> folders = await GoogleWorkspaceBrowserService.ListDriveFoldersAsync(currentFolder.ID, CancellationToken.None);
            mFolders = folders.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Drive 폴더를 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task loadCalendarsAsync()
    {
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
        try
        {
            IReadOnlyList<GoogleTaskListItem> taskLists = await GoogleWorkspaceBrowserService.ListTaskListsAsync(CancellationToken.None);
            mTaskLists = taskLists.ToList();
        }
        catch (Exception exception)
        {
            mMessage = createGoogleTasksErrorMessage(exception);
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task disconnectGoogleAsync()
    {
        ServiceResult result = await GoogleConnectionService.DisconnectAsync(CancellationToken.None);
        showResult(result, "Google 계정 연결이 해제되었습니다.");
        await loadStatusAsync();
        mFolders = new List<GoogleDriveFolderItem>();
        mCalendars = new List<GoogleCalendarListItem>();
        mTaskLists = new List<GoogleTaskListItem>();
        await loadHealthCheckAsync();
    }

    private async Task openFolderAsync(GoogleDriveFolderItem folder)
    {
        GoogleDriveFolderPathItem pathItem = new GoogleDriveFolderPathItem();
        pathItem.ID = folder.ID;
        pathItem.Name = folder.Name;
        mFolderPath.Add(pathItem);
        await loadCurrentFolderAsync();
    }

    private async Task openPathAsync(int index)
    {
        if (index < 0 || index >= mFolderPath.Count)
        {
            return;
        }

        mFolderPath = mFolderPath.Take(index + 1).ToList();
        await loadCurrentFolderAsync();
    }

    private async Task assignFolderAsync(GoogleDriveFolderItem folder)
    {
        ServiceResult result = await GoogleWorkspaceSetupService.AssignFolderAsync(mSelectedFolderTargetKey, folder.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? folder.Name + " 폴더를 저장했습니다." : string.Empty);
        await loadHealthCheckAsync();
    }

    private async Task assignCalendarAsync(GoogleCalendarListItem calendar)
    {
        ServiceResult result = await GoogleWorkspaceSetupService.AssignCalendarAsync(mSelectedCalendarTargetKey, calendar.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? calendar.Summary + " Calendar를 저장했습니다." : string.Empty);
        await loadHealthCheckAsync();
    }

    private async Task assignTaskListAsync(GoogleTaskListItem taskList)
    {
        ServiceResult result = await GoogleWorkspaceSetupService.AssignTaskListAsync(mSelectedTaskListTargetKey, taskList.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? taskList.Title + " Google Tasks 목록을 저장했습니다." : string.Empty);
        await loadHealthCheckAsync();
    }

    private async Task ensureStandardDriveFoldersAsync()
    {
        mIsEnsuringStandardDriveFolders = true;
        try
        {
            ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> result = await GoogleWorkspaceSetupService.EnsureStandardDriveFoldersAsync(CancellationToken.None);
            if (result.IsSuccess && result.ValueOrNull != null)
            {
                GoogleWorkspaceResourceSetupSummary summary = result.ValueOrNull;
                mMessage = createSetupSummaryMessage("기본 Drive 폴더 준비 완료", summary);
                mMessageSeverity = Severity.Success;
                resetFolderPath();
                await loadAssignmentOptionsAsync();
                await loadCurrentFolderAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = result.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsEnsuringStandardDriveFolders = false;
        }
    }

    private async Task ensureStandardCalendarsAsync()
    {
        mIsEnsuringStandardCalendars = true;
        try
        {
            ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> result = await GoogleWorkspaceSetupService.EnsureStandardCalendarsAsync(CancellationToken.None);
            if (result.IsSuccess && result.ValueOrNull != null)
            {
                GoogleWorkspaceResourceSetupSummary summary = result.ValueOrNull;
                mMessage = createSetupSummaryMessage("기본 Calendar 준비 완료", summary);
                mMessageSeverity = Severity.Success;
                await loadAssignmentOptionsAsync();
                await loadCalendarsAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = result.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsEnsuringStandardCalendars = false;
        }
    }

    private async Task applyStandardDriveFolderNamesAsync()
    {
        mIsApplyingStandardDriveFolderNames = true;
        try
        {
            ServiceResultOfT<GoogleDriveFolderNameSyncSummary> result = await GoogleWorkspaceSetupService.ApplyStandardDriveFolderNamesAsync(CancellationToken.None);
            if (result.IsSuccess && result.ValueOrNull != null)
            {
                GoogleDriveFolderNameSyncSummary summary = result.ValueOrNull;
                mMessage = "Drive 폴더 정리 완료: 이름 변경 " + summary.RenamedCount.ToString(CultureInfo.InvariantCulture) + "개, 위치 정리 " + summary.MovedCount.ToString(CultureInfo.InvariantCulture) + "개, 폴더 생성 " + summary.CreatedCount.ToString(CultureInfo.InvariantCulture) + "개, 이미 표준 " + summary.AlreadyStandardCount.ToString(CultureInfo.InvariantCulture) + "개";
                mMessageSeverity = Severity.Success;
                await loadCurrentFolderAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = result.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsApplyingStandardDriveFolderNames = false;
        }
    }

    private async Task ensureStandardTaskListsAsync()
    {
        mIsEnsuringStandardTaskLists = true;
        try
        {
            ServiceResultOfT<GoogleTaskListSetupSummary> result = await GoogleWorkspaceSetupService.EnsureStandardTaskListsAsync(CancellationToken.None);
            if (result.IsSuccess && result.ValueOrNull != null)
            {
                GoogleTaskListSetupSummary summary = result.ValueOrNull;
                mMessage = "기본 Google Tasks 목록 준비 완료: 생성 " + summary.CreatedCount.ToString(CultureInfo.InvariantCulture) + "개, 재사용 " + summary.ReusedCount.ToString(CultureInfo.InvariantCulture) + "개, 매핑 " + summary.AssignedCount.ToString(CultureInfo.InvariantCulture) + "개";
                mMessageSeverity = Severity.Success;
                await loadTaskListsAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = result.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsEnsuringStandardTaskLists = false;
        }
    }

    private async Task loadHealthCheckAsync()
    {
        try
        {
            IReadOnlyList<GoogleHealthCheckItem> healthItems = await GoogleHealthCheckService.GetHealthCheckItemsAsync(CancellationToken.None);
            mHealthItems = healthItems.ToList();
            mShowHealthyHealthItems = false;
            if (mHealthItems.Count == 0)
            {
                GoogleHealthCheckItem healthCheckItem = new GoogleHealthCheckItem();
                healthCheckItem.Area = "진단";
                healthCheckItem.Name = "결과 없음";
                healthCheckItem.Status = "Warning";
                healthCheckItem.Message = "진단 항목을 만들지 못했습니다. Google 연결 정보와 연결 상태를 새로고침해 주세요.";
                mHealthItems.Add(healthCheckItem);
            }
        }
        catch (Exception exception)
        {
            mHealthItems = new List<GoogleHealthCheckItem>();
            GoogleHealthCheckItem healthCheckItem = new GoogleHealthCheckItem();
            healthCheckItem.Area = "진단";
            healthCheckItem.Name = "실행 오류";
            healthCheckItem.Status = "Error";
            healthCheckItem.Message = "Google 연결 진단을 실행하지 못했습니다. " + exception.Message;
            mHealthItems.Add(healthCheckItem);
            mMessage = "Google 연결 진단을 실행하지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private IReadOnlyList<GoogleHealthCheckItem> getAttentionHealthItems()
    {
        return mHealthItems.Where(item => isHealthyHealthStatus(item.Status) == false).ToList();
    }

    private IReadOnlyList<GoogleHealthCheckItem> getHealthyHealthItems()
    {
        return mHealthItems.Where(item => isHealthyHealthStatus(item.Status)).ToList();
    }

    private bool isHealthyHealthStatus(string status)
    {
        return status == "Healthy";
    }

    private void toggleHealthyHealthItems()
    {
        mShowHealthyHealthItems = !mShowHealthyHealthItems;
    }

    private string getHealthSummaryClass()
    {
        if (mHealthItems.Any(item => item.Status == "Error"))
        {
            return "hub-health-error";
        }

        if (mHealthItems.Any(item => item.Status == "Warning"))
        {
            return "hub-health-warning";
        }

        return "hub-health-healthy";
    }

    private string getHealthSummaryText()
    {
        int errorCount = mHealthItems.Count(item => item.Status == "Error");
        int warningCount = mHealthItems.Count(item => item.Status == "Warning");
        if (errorCount > 0)
        {
            return "오류 " + errorCount + "개";
        }

        if (warningCount > 0)
        {
            return "주의 " + warningCount + "개";
        }

        return "정상";
    }

    private string getHealthSummaryDescription()
    {
        int attentionCount = getAttentionHealthItems().Count;
        int healthyCount = getHealthyHealthItems().Count;
        if (attentionCount > 0)
        {
            return "먼저 확인할 항목 " + attentionCount + "개, 정상 항목 " + healthyCount + "개";
        }

        return "정상 항목 " + healthyCount + "개가 접혀 있습니다.";
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

    private string createGoogleTasksErrorMessage(Exception exception)
    {
        return GoogleApiExceptionMessageFormatter.CreateGoogleTasksMessage(exception, "Google Tasks 목록을 불러오지 못했습니다.");
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

    private void resetFolderPath()
    {
        GoogleDriveFolderPathItem rootPathItem = new GoogleDriveFolderPathItem();
        rootPathItem.ID = "root";
        rootPathItem.Name = "내 Drive";
        mFolderPath = new List<GoogleDriveFolderPathItem>();
        mFolderPath.Add(rootPathItem);
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

    private string getHealthClass(string status)
    {
        switch (status)
        {
            case "Healthy":
                return "hub-health-healthy";
            case "Warning":
                return "hub-health-warning";
            default:
                return "hub-health-error";
        }
    }

    private string getHealthText(string status)
    {
        switch (status)
        {
            case "Healthy":
                return "정상";
            case "Warning":
                return "주의";
            default:
                return "오류";
        }
    }

    private string getCalendarAccessDisplayName(GoogleCalendarListItem calendar)
    {
        if (calendar.IsPrimary)
        {
            return "기본 Calendar";
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
}
