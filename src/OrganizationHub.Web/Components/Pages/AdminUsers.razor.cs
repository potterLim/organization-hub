using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminUsers
{
    private List<UserAccount> mUsers = new List<UserAccount>();
    private List<Team> mTeams = new List<Team>();
    private List<OrganizationPosition> mPositions = new List<OrganizationPosition>();
    private UserAccountFormModel mFormModel = new UserAccountFormModel();
    private string mFormTitle = "새 사용자";
    private string mResetPassword = string.Empty;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;
    private bool mIsSystemAdministratorSelected;
    private bool mCanManageSystemAdmins;
    private bool mHasSaveConflict;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await tryLoadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        mCanManageSystemAdmins = await UserManagementService.CanManageSystemAdministratorsAsync(CurrentUserSessionOrNull, CancellationToken.None);
        clearForm();
        await loadDataAsync();
    }
}
