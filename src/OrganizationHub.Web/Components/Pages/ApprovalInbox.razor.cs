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
    private List<ApprovalUserOption> mApproverOptions = new List<ApprovalUserOption>();
    private readonly Dictionary<int, string> mCommentsByApprovalId = new Dictionary<int, string>();
    private readonly Dictionary<int, int> mNextApproverUserIdByApprovalId = new Dictionary<int, int>();
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mIsLoading = true;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        ApprovalCreateContext createContext = await ApprovalService.GetCreateContextAsync(CurrentUserSessionOrNull, CancellationToken.None);
        mApproverOptions = createContext.ApproverOptions.ToList();
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
        mPendingApprovals = pendingApprovals.ToList();
        foreach (ApprovalRequestItem approvalRequest in mPendingApprovals)
        {
            ensureLocalStateForApprovalRequest(approvalRequest);
        }
    }
}
