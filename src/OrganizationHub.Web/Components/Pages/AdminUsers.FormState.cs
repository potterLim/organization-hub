using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminUsers
{
    private void editUser(UserAccount userAccount)
    {
        if (canManageUser(userAccount) == false)
        {
            return;
        }

        mFormModel = new UserAccountFormModel();
        mFormModel.Id = userAccount.Id;
        mFormModel.Username = userAccount.Username;
        mFormModel.DisplayName = userAccount.DisplayName;
        mFormModel.Role = userAccount.Role;
        mFormModel.TeamId = userAccount.TeamId;
        mFormModel.PositionId = userAccount.PositionId;
        mFormModel.IsActive = userAccount.IsActive;
        mapRoleToFormFields(userAccount.Role);
        ensureDefaultTeamSelection();
        ensureDefaultPositionSelection();
        mFormTitle = "사용자 수정";
        mResetPassword = string.Empty;
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

    private Task handleTeamChangedAsync(int? teamIdOrNull)
    {
        mFormModel.TeamId = teamIdOrNull;
        ensureDefaultPositionSelection();
        return Task.CompletedTask;
    }

    private Task handlePositionChangedAsync(int? positionIdOrNull)
    {
        mFormModel.PositionId = positionIdOrNull;
        return Task.CompletedTask;
    }

    private void mapRoleToFormFields(EUserRole role)
    {
        mIsSystemAdmin = role == EUserRole.Admin;
        if (mIsSystemAdmin)
        {
            mFormModel.TeamId = getAdministrationTeamIdOrNull();
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
            mFormModel.TeamId = getAdministrationTeamIdOrNull();
            mFormModel.PositionId = null;
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

        bool selectedTeamIsAvailable = mFormModel.TeamId.HasValue && mTeams.Any(team => team.Id == mFormModel.TeamId.Value);
        if (selectedTeamIsAvailable)
        {
            ensureDefaultPositionSelection();
            return;
        }

        Team? firstTeamOrNull = getSelectableTeams().FirstOrDefault();
        mFormModel.TeamId = firstTeamOrNull == null ? null : firstTeamOrNull.Id;
        ensureDefaultPositionSelection();
    }

    private void ensureDefaultPositionSelection()
    {
        if (mIsSystemAdmin)
        {
            mFormModel.PositionId = null;
            return;
        }

        IReadOnlyList<OrganizationPosition> selectablePositions = getSelectablePositions();
        bool selectedPositionIsAvailable = mFormModel.PositionId.HasValue && selectablePositions.Any(position => position.Id == mFormModel.PositionId.Value);
        if (selectedPositionIsAvailable)
        {
            return;
        }

        OrganizationPosition? firstPositionOrNull = selectablePositions.FirstOrDefault();
        mFormModel.PositionId = firstPositionOrNull == null ? null : firstPositionOrNull.Id;
    }
}
