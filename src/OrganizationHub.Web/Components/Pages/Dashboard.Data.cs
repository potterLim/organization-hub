using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Dashboard
{
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
            IReadOnlyList<DriveFileItem> recentMaterials = await DriveBrowserService.GetRecentUploadedFilesAsync(CurrentUserSessionOrNull, EDriveFolderKind.TeamMaterials, new RecentItemLimit(5), EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);
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
}
