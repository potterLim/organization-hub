using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class Dashboard
{
    private const int DASHBOARD_PENDING_APPROVAL_MAXIMUM_COUNT = 3;
    private const int DASHBOARD_TODO_MAXIMUM_COUNT = 12;
    private const int DASHBOARD_WEEK_EVENT_MAXIMUM_COUNT = 6;
    private const int DASHBOARD_RECENT_MATERIAL_MAXIMUM_COUNT = 5;

    private static readonly TimeSpan RECENT_MATERIALS_LOAD_TIMEOUT = TimeSpan.FromSeconds(8);

    private async Task loadDashboardSectionsAsync()
    {
        Task dashboardSettingsTask = loadDashboardSettingsAsync();
        Task pendingApprovalsTask = mAccessOrNull != null && (mAccessOrNull.CanOpenInbox || mAccessOrNull.CanOpenHistory) ? loadPendingApprovalsAsync() : Task.CompletedTask;
        Task weekEventsTask = mAccessOrNull != null && mAccessOrNull.CanOpenCalendar ? loadWeekEventsAsync() : Task.CompletedTask;
        Task weekTodosTask = mAccessOrNull != null && mAccessOrNull.CanOpenTodos ? loadWeekTodosAsync() : Task.CompletedTask;
        Task recentMaterialsTask = mAccessOrNull != null && mAccessOrNull.CanOpenMaterials ? loadRecentMaterialsAsync() : Task.CompletedTask;
        await Task.WhenAll(dashboardSettingsTask, pendingApprovalsTask, weekEventsTask, weekTodosTask, recentMaterialsTask);
    }

    private async Task loadDashboardSettingsAsync()
    {
        try
        {
            GlobalSettingFormModel globalSetting = await GlobalSettingService.GetGlobalSettingAsync(CancellationToken.None);
            mShouldShowCalendarAuthor = globalSetting.ShouldShowCalendarAuthor;
            mShouldShowTodoAuthor = globalSetting.ShouldShowTodoAuthor;
            GoogleConnectionStatus connectionStatus = await GoogleConnectionService.GetStatusAsync(CancellationToken.None);
            mIsWorkspaceConnected = connectionStatus.IsConnected;
            mHasWorkspaceRootFolder = globalSetting.DriveResources.OfficialRootFolderIdValue.HasValue;
            mHasLoadedWorkspaceSetup = true;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Dashboard setting load failed.");
            mShouldShowCalendarAuthor = false;
            mShouldShowTodoAuthor = true;
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
            if (mAccessOrNull != null && mAccessOrNull.CanOpenInbox)
            {
                IReadOnlyList<ApprovalRequestItem> pendingApprovals = await ApprovalService.GetPendingApprovalsAsync(CurrentUserSessionOrNull, CancellationToken.None);
                mPendingApprovals = pendingApprovals.Take(DASHBOARD_PENDING_APPROVAL_MAXIMUM_COUNT).ToList();
            }
            else
            {
                mOwnProgressOrNull = await ApprovalService.GetOwnProgressAsync(CurrentUserSessionOrNull, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Dashboard pending approval load failed.");
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
            DateTime today = ClientTimeZoneUtility.getClientNow(mClientTimeZoneInfo).Date;
            DateTime weekStart = CalendarWeekUtility.GetStartOfWeek(today);
            DateTime weekEnd = CalendarWeekUtility.GetEndOfWeek(today);
            CalendarEventDateRange dateRange = new CalendarEventDateRange(weekStart, weekEnd);
            IReadOnlyList<CalendarEventItem> events = await CalendarService.GetAggregateEventsAsync(CurrentUserSessionOrNull, dateRange, CancellationToken.None);
            mWeekEvents = events.Take(DASHBOARD_WEEK_EVENT_MAXIMUM_COUNT).ToList();
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Dashboard weekly event load failed.");
            mWeekEvents = new List<CalendarEventItem>();
            mWeekEventsErrorMessage = "이번 주 일정을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        mIsLoadingWeekEvents = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task loadWeekTodosAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            mIsLoadingWeekTodos = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        mIsLoadingWeekTodos = true;
        mWeekTodosErrorMessage = string.Empty;
        await InvokeAsync(StateHasChanged);
        try
        {
            IReadOnlyList<TodoTaskItem> tasks = await TodoService.GetTasksAsync(CurrentUserSessionOrNull, TodoTaskListId.AllTasks, CancellationToken.None);
            mWeekTodos = createDashboardTodos(tasks);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Dashboard weekly todo load failed.");
            mWeekTodos = new List<TodoTaskItem>();
            mWeekTodosErrorMessage = "이번 주 할 일을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        mIsLoadingWeekTodos = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task loadRecentMaterialsAsync()
    {
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null)
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
            IReadOnlyList<DriveFileItem>? recentMaterialsOrNull = await tryLoadRecentMaterialsWithTimeoutAsync(currentUserSessionOrNull);
            if (recentMaterialsOrNull == null)
            {
                mRecentMaterials = new List<DriveFileItem>();
                mRecentMaterialsErrorMessage = "최근 팀 자료 확인이 지연되고 있습니다. 전체 보기에서 다시 확인해 주세요.";
            }
            else
            {
                mRecentMaterials = recentMaterialsOrNull.ToList();
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Dashboard recent material load timed out.");
            mRecentMaterials = new List<DriveFileItem>();
            mRecentMaterialsErrorMessage = "최근 팀 자료 확인이 지연되고 있습니다. 전체 보기에서 다시 확인해 주세요.";
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Dashboard recent material load failed.");
            mRecentMaterials = new List<DriveFileItem>();
            mRecentMaterialsErrorMessage = "최근 팀 자료를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }

        mIsLoadingRecentMaterials = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<IReadOnlyList<DriveFileItem>?> tryLoadRecentMaterialsWithTimeoutAsync(UserSession currentUserSession)
    {
        CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource();
        Task<IReadOnlyList<DriveFileItem>> recentMaterialsTask = DriveBrowserService.GetRecentUploadedFilesAsync(
            currentUserSession,
            EDriveFolderKind.TeamMaterials,
            new RecentItemLimit(DASHBOARD_RECENT_MATERIAL_MAXIMUM_COUNT),
            EManagementTeamFilter.ExcludeManagementTeam,
            timeoutCancellationTokenSource.Token);
        Task completedTask = await Task.WhenAny(recentMaterialsTask, Task.Delay(RECENT_MATERIALS_LOAD_TIMEOUT));
        if (completedTask != recentMaterialsTask)
        {
            timeoutCancellationTokenSource.Cancel();
            mRecentMaterialsObservationTaskOrNull = observeTaskAndDisposeCancellationSourceAsync(
                recentMaterialsTask,
                timeoutCancellationTokenSource,
                "Dashboard recent material load failed after timeout fallback.");
            return null;
        }

        try
        {
            return await recentMaterialsTask;
        }
        finally
        {
            timeoutCancellationTokenSource.Dispose();
        }
    }

    private async Task observeFaultedTaskAsync(Task task, string warningMessage)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("{Message}", warningMessage);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "{Message}", warningMessage);
        }
    }

    private async Task observeTaskAndDisposeCancellationSourceAsync(
        Task task,
        CancellationTokenSource cancellationTokenSource,
        string warningMessage
    )
    {
        try
        {
            await observeFaultedTaskAsync(task, warningMessage);
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    private List<TodoTaskItem> createDashboardTodos(IReadOnlyList<TodoTaskItem> tasks)
    {
        DateTime today = ClientTimeZoneUtility.getClientNow(mClientTimeZoneInfo).Date;
        DateTime weekEnd = CalendarWeekUtility.GetEndOfWeek(today);
        return tasks
            .Where(task => task.IsCompleted == false)
            .Where(task => isTaskDueForDashboard(task, weekEnd))
            .OrderBy(task => getTaskDueDisplayDateOrNull(task))
            .ThenBy(task => task.TaskListDisplayName)
            .ThenBy(task => task.Position)
            .ThenBy(task => task.Title)
            .Take(DASHBOARD_TODO_MAXIMUM_COUNT)
            .ToList();
    }

    private bool isTaskDueForDashboard(TodoTaskItem task, DateTime weekEnd)
    {
        DateTime? dueAtOrNull = getTaskDueDisplayDateOrNull(task);
        if (dueAtOrNull.HasValue == false)
        {
            return false;
        }

        DateTime dueDate = dueAtOrNull.Value.Date;
        return dueDate <= weekEnd.Date;
    }

    private DateTime? getTaskDueDisplayDateOrNull(TodoTaskItem task)
    {
        if (task.DueAt.HasValue == false)
        {
            return null;
        }

        return ClientTimeZoneUtility.createClientDateTime(task.DueAt.Value, mClientTimeZoneInfo);
    }
}
