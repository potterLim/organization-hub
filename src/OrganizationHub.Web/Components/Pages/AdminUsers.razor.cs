using System.Collections.Generic;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
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
    private bool mIsSystemAdmin;
    private bool mCanManageSystemAdmins;

    protected override async Task OnInitializedAsync()
    {
        bool hasSession = await loadUserSessionAsync();
        if (hasSession == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        mCanManageSystemAdmins = UserPermissionPolicy.CanManageSystemAdministratorAccounts(CurrentUserSessionOrNull.Role);
        clearForm();
        await loadDataAsync();
    }
}
