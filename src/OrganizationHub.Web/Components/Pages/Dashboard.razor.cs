using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Dashboard
{
    private List<DriveFileItem> mRecentMaterials = new List<DriveFileItem>();
    private List<CalendarEventItem> mWeekEvents = new List<CalendarEventItem>();
    private List<TodoTaskItem> mWeekTodos = new List<TodoTaskItem>();
    private List<ApprovalRequestItem> mPendingApprovals = new List<ApprovalRequestItem>();
    private TimeZoneInfo mClientTimeZoneInfo = TimeZoneInfo.Local;
    private string mPendingApprovalsErrorMessage = string.Empty;
    private string mWeekEventsErrorMessage = string.Empty;
    private string mWeekTodosErrorMessage = string.Empty;
    private string mRecentMaterialsErrorMessage = string.Empty;
    private bool mIsLoadingPendingApprovals = true;
    private bool mIsLoadingWeekEvents = true;
    private bool mIsLoadingWeekTodos = true;
    private bool mIsLoadingRecentMaterials = true;
    private bool mShouldShowCalendarAuthor;
    private bool mHasLoadedWorkspaceSetup;
    private DashboardAccess? mAccessOrNull;
    private OwnApprovalProgress? mOwnProgressOrNull;
    private bool mIsWorkspaceConnected;
    private bool mHasWorkspaceRootFolder;
    private bool mShouldShowTodoAuthor = true;
    private Task? mDashboardSectionsObservationTaskOrNull;
    private Task? mRecentMaterialsObservationTaskOrNull;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await tryLoadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        mAccessOrNull = await DashboardAccessService.GetAccessAsync(CurrentUserSessionOrNull, CancellationToken.None);
        Task dashboardSectionsTask = loadDashboardSectionsAsync();
        mDashboardSectionsObservationTaskOrNull = observeFaultedTaskAsync(
            dashboardSectionsTask,
            "Dashboard section loading failed after the home page was rendered.");
    }
}
