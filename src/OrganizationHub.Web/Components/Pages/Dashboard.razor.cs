using System.Collections.Generic;
using System.Threading.Tasks;
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
}
