using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    private async Task loadDataAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        IReadOnlyList<UserAccount> users = await UserManagementService.GetUsersAsync(CurrentUserSessionOrNull, CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        IReadOnlyList<OrganizationPosition> positions = await PositionManagementService.GetPositionsAsync(CancellationToken.None);
        mUsers = users.ToList();
        mTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
        mPositions = positions.Where(position => position.IsActive).OrderBy(position => position.Team == null ? 0 : position.Team.SortOrder).ThenBy(position => position.SortOrder).ThenBy(position => position.Name).ToList();
        ensureDefaultTeamSelection();
    }

    private async Task saveUserAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        syncFormRoleAndTeam();
        ServiceResult result = mFormModel.ID == 0 ? await UserManagementService.CreateUserAsync(CurrentUserSessionOrNull, mFormModel, CancellationToken.None) : await UserManagementService.UpdateUserAsync(CurrentUserSessionOrNull, mFormModel, CancellationToken.None);
        showResult(result, "사용자 정보가 저장되었습니다.");
        if (result.IsSuccess)
        {
            clearForm();
            await loadDataAsync();
        }
    }

    private void editUser(UserAccount userAccount)
    {
        if (canManageUser(userAccount) == false)
        {
            return;
        }

        mFormModel = new UserAccountFormModel();
        mFormModel.ID = userAccount.ID;
        mFormModel.Username = userAccount.Username;
        mFormModel.DisplayName = userAccount.DisplayName;
        mFormModel.Role = userAccount.Role;
        mFormModel.TeamID = userAccount.TeamID;
        mFormModel.PositionID = userAccount.PositionID;
        mFormModel.IsActive = userAccount.IsActive;
        mapRoleToFormFields(userAccount.Role);
        ensureDefaultTeamSelection();
        ensureDefaultPositionSelection();
        mFormTitle = "사용자 수정";
        mResetPassword = string.Empty;
    }

    private async Task toggleUserActiveAsync(UserAccount userAccount)
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        ServiceResult result = userAccount.IsActive ? await UserManagementService.DeactivateUserAsync(CurrentUserSessionOrNull, userAccount.ID, CancellationToken.None) : await UserManagementService.ActivateUserAsync(CurrentUserSessionOrNull, userAccount.ID, CancellationToken.None);
        showResult(result, "사용자 상태가 변경되었습니다.");
        await loadDataAsync();
    }

    private async Task resetPasswordAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        ServiceResult result = await UserManagementService.ResetPasswordAsync(CurrentUserSessionOrNull, mFormModel.ID, mResetPassword, CancellationToken.None);
        showResult(result, "비밀번호가 초기화되었습니다.");
        if (result.IsSuccess)
        {
            mResetPassword = string.Empty;
        }
    }

    private void clearForm()
    {
        mFormModel = new UserAccountFormModel();
        mFormModel.Role = EUserRole.TeamUser;
        mFormModel.IsActive = true;
        mIsSystemAdmin = false;
        ensureDefaultTeamSelection();
        ensureDefaultPositionSelection();
        mFormTitle = "새 사용자";
        mResetPassword = string.Empty;
    }

    private Task handleTeamChangedAsync(int? teamIDOrNull)
    {
        mFormModel.TeamID = teamIDOrNull;
        ensureDefaultPositionSelection();
        return Task.CompletedTask;
    }

    private Task handlePositionChangedAsync(int? positionIDOrNull)
    {
        mFormModel.PositionID = positionIDOrNull;
        return Task.CompletedTask;
    }

    private void mapRoleToFormFields(EUserRole role)
    {
        mIsSystemAdmin = role == EUserRole.Admin;
        if (mIsSystemAdmin)
        {
            mFormModel.TeamID = getAdministrationTeamIDOrNull();
            return;
        }

        ensureDefaultTeamSelection();
    }

    private void syncFormRoleAndTeam()
    {
        if (mCanManageSystemAdmins == false)
        {
            mIsSystemAdmin = false;
        }

        if (mIsSystemAdmin)
        {
            mFormModel.Role = EUserRole.Admin;
            mFormModel.TeamID = getAdministrationTeamIDOrNull();
            mFormModel.PositionID = null;
            return;
        }

        ensureDefaultTeamSelection();
        ensureDefaultPositionSelection();
        mFormModel.Role = getDefaultOrganizationMemberRole();
    }

    private void ensureDefaultTeamSelection()
    {
        if (mIsSystemAdmin)
        {
            return;
        }

        bool selectedTeamIsAvailable = mFormModel.TeamID.HasValue && mTeams.Any(team => team.ID == mFormModel.TeamID.Value);
        if (selectedTeamIsAvailable)
        {
            ensureDefaultPositionSelection();
            return;
        }

        Team? firstTeamOrNull = getSelectableTeams().FirstOrDefault();
        mFormModel.TeamID = firstTeamOrNull?.ID;
        ensureDefaultPositionSelection();
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

    private string getAffiliationName(UserAccount userAccount)
    {
        if (userAccount.Role == EUserRole.Admin)
        {
            return "시스템";
        }

        if (UserRoleUtility.GetAffiliation(userAccount.Role) == EUserAffiliation.Management)
        {
            return OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME;
        }

        if (userAccount.Team == null)
        {
            return "팀";
        }

        return userAccount.Team.DisplayName;
    }

    private string getPositionName(UserAccount userAccount)
    {
        if (userAccount.Position != null)
        {
            return userAccount.Position.Name;
        }

        return UserRoleUtility.GetDisplayName(userAccount.Role);
    }

    private bool isSystemAdministrationTeam(Team team)
    {
        return team.TeamKey.Equals(OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<Team> getSelectableTeams()
    {
        return mTeams.Where(team => isSystemAdministrationTeam(team) == false).ToList();
    }

    private IReadOnlyList<OrganizationPosition> getSelectablePositions()
    {
        if (mFormModel.TeamID.HasValue == false)
        {
            return new List<OrganizationPosition>();
        }

        List<OrganizationPosition> positions = mPositions
            .Where(position => position.TeamID == mFormModel.TeamID.Value && position.IsSystemAdministrator == false)
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .ToList();
        return positions;
    }

    private int? getAdministrationTeamIDOrNull()
    {
        Team? administrationTeamOrNull = mTeams.FirstOrDefault(team => isSystemAdministrationTeam(team));
        if (administrationTeamOrNull == null)
        {
            return null;
        }

        return administrationTeamOrNull.ID;
    }

    private void ensureDefaultPositionSelection()
    {
        if (mIsSystemAdmin)
        {
            mFormModel.PositionID = null;
            return;
        }

        IReadOnlyList<OrganizationPosition> selectablePositions = getSelectablePositions();
        bool selectedPositionIsAvailable = mFormModel.PositionID.HasValue && selectablePositions.Any(position => position.ID == mFormModel.PositionID.Value);
        if (selectedPositionIsAvailable)
        {
            return;
        }

        OrganizationPosition? firstPositionOrNull = selectablePositions.FirstOrDefault();
        mFormModel.PositionID = firstPositionOrNull?.ID;
    }

    private EUserRole getDefaultOrganizationMemberRole()
    {
        return EUserRole.TeamUser;
    }

    private bool canManageUser(UserAccount userAccount)
    {
        if (userAccount.Role == EUserRole.Admin)
        {
            return mCanManageSystemAdmins;
        }

        return true;
    }
}
