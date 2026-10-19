using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private List<ApprovalRequestItem> mPendingApprovals = new List<ApprovalRequestItem>();
    private readonly Dictionary<int, string> mCommentsByApprovalId = new Dictionary<int, string>();
    private readonly Dictionary<int, DriveFileNameConflictCheckResult> mApprovedFileConflictsByApprovalId = new Dictionary<int, DriveFileNameConflictCheckResult>();
    private readonly HashSet<int> mProcessingApprovalIds = new HashSet<int>();
    private readonly Dictionary<int, string> mErrorsByApprovalId = new Dictionary<int, string>();
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mIsLoading = true;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await tryLoadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        await loadDataAsync();
        mIsLoading = false;
    }

    private async Task loadDataAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        IReadOnlyList<ApprovalRequestItem> pendingApprovals = await ApprovalService.GetPendingApprovalsAsync(CurrentUserSessionOrNull, CancellationToken.None);
        mPendingApprovals = pendingApprovals.OrderBy(request => request.Status == OrganizationHub.Web.Domain.EApprovalRequestStatus.Publishing).ToList();
        foreach (ApprovalRequestItem approvalRequest in mPendingApprovals)
        {
            ensureLocalStateForApprovalRequest(approvalRequest);
        }
    }
}
