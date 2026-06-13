using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Dashboard
{
    private List<DriveFileItem> mRecentMaterials = new List<DriveFileItem>();
    private List<CalendarEventItem> mWeekEvents = new List<CalendarEventItem>();
    private List<ApprovalRequestItem> mPendingApprovals = new List<ApprovalRequestItem>();
    private string mPendingApprovalsErrorMessage = string.Empty;
    private string mWeekEventsErrorMessage = string.Empty;
    private string mRecentMaterialsErrorMessage = string.Empty;
    private bool mIsLoadingPendingApprovals = true;
    private bool mIsLoadingWeekEvents = true;
    private bool mIsLoadingRecentMaterials = true;
    private bool mShowCalendarAuthor;
    private bool mCanCreateApprovalRequest;
    private bool mCanUploadFiles;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        await loadQuickActionPermissionsAsync();
        _ = loadDashboardSectionsAsync();
    }

    private async Task loadQuickActionPermissionsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            mCanCreateApprovalRequest = false;
            mCanUploadFiles = false;
            return;
        }

        try
        {
            ApprovalCreateContext approvalCreateContext = await ApprovalService.GetCreateContextAsync(CurrentUserSessionOrNull, CancellationToken.None);
            mCanCreateApprovalRequest = approvalCreateContext.CanCreateRequest;
        }
        catch
        {
            mCanCreateApprovalRequest = false;
        }

        try
        {
            IReadOnlyList<DriveFolderReference> uploadableFolders = await PermissionService.GetUploadableDriveFoldersAsync(CurrentUserSessionOrNull, CancellationToken.None);
            mCanUploadFiles = uploadableFolders.Count > 0;
        }
        catch
        {
            mCanUploadFiles = false;
        }
    }

    private async Task loadDashboardSectionsAsync()
    {
        Task dashboardSettingsTask = loadDashboardSettingsAsync();
        Task pendingApprovalsTask = loadPendingApprovalsAsync();
        Task weekEventsTask = loadWeekEventsAsync();
        Task recentMaterialsTask = loadRecentMaterialsAsync();
        await Task.WhenAll(dashboardSettingsTask, pendingApprovalsTask, weekEventsTask, recentMaterialsTask);
    }

    private async Task loadDashboardSettingsAsync()
    {
        try
        {
            GlobalSettingFormModel globalSetting = await TeamManagementService.GetGlobalSettingAsync(CancellationToken.None);
            mShowCalendarAuthor = globalSetting.ShowCalendarAuthor;
        }
        catch
        {
            mShowCalendarAuthor = false;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task loadPendingApprovalsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            mIsLoadingPendingApprovals = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        mIsLoadingPendingApprovals = true;
        mPendingApprovalsErrorMessage = string.Empty;
        await InvokeAsync(StateHasChanged);
        try
        {
            IReadOnlyList<ApprovalRequestItem> pendingApprovals = await ApprovalService.GetPendingApprovalsAsync(CurrentUserSessionOrNull, CancellationToken.None);
            mPendingApprovals = pendingApprovals.Take(3).ToList();
        }
        catch
        {
            mPendingApprovals = new List<ApprovalRequestItem>();
            mPendingApprovalsErrorMessage = "결재 대기 목록을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        mIsLoadingPendingApprovals = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task loadWeekEventsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            mIsLoadingWeekEvents = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        mIsLoadingWeekEvents = true;
        mWeekEventsErrorMessage = string.Empty;
        await InvokeAsync(StateHasChanged);
        try
        {
            DateTime today = DateTime.Today;
            DateTime weekStart = CalendarWeekUtility.GetStartOfWeek(today);
            DateTime weekEnd = CalendarWeekUtility.GetEndOfWeek(today);
            IReadOnlyList<CalendarEventItem> events = await CalendarService.GetAggregateEventsAsync(CurrentUserSessionOrNull, weekStart, weekEnd, CancellationToken.None);
            mWeekEvents = events.Take(6).ToList();
        }
        catch
        {
            mWeekEvents = new List<CalendarEventItem>();
            mWeekEventsErrorMessage = "이번 주 일정을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        mIsLoadingWeekEvents = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task loadRecentMaterialsAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            mIsLoadingRecentMaterials = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        mIsLoadingRecentMaterials = true;
        mRecentMaterialsErrorMessage = string.Empty;
        await InvokeAsync(StateHasChanged);
        try
        {
            IReadOnlyList<DriveFileItem> recentMaterials = await DriveBrowserService.GetRecentUploadedFilesAsync(CurrentUserSessionOrNull, EDriveFolderKind.TeamMaterials, 5, EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);
            mRecentMaterials = recentMaterials.ToList();
        }
        catch
        {
            mRecentMaterials = new List<DriveFileItem>();
            mRecentMaterialsErrorMessage = "최근 팀 자료를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        mIsLoadingRecentMaterials = false;
        await InvokeAsync(StateHasChanged);
    }

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

    private string formatDate(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
