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
        mHasSaveConflict = false;
        mFormModel.Id = userAccount.Id;
        mFormModel.Revision = userAccount.Revision;
        mFormModel.Username = userAccount.Username;
        mFormModel.DisplayName = userAccount.DisplayName;
        mFormModel.TeamId = userAccount.TeamId;
        mFormModel.PositionId = userAccount.PositionId;
        mFormModel.IsActive = userAccount.IsActive;
        mapUserKindToFormFields(userAccount);
        ensureDefaultTeamSelection();
        ensureDefaultPositionSelection();
        mFormTitle = "사용자 수정";
        mResetPassword = string.Empty;
    }

    private void clearForm()
    {
        mHasSaveConflict = false;
        mFormModel = new UserAccountFormModel();
        mFormModel.AccountKind = EUserAccountKind.OrganizationMember;
        mFormModel.IsActive = true;
        mIsSystemAdministratorSelected = false;
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

    private void mapUserKindToFormFields(UserAccount userAccount)
    {
        mIsSystemAdministratorSelected = isSystemAdministratorAccount(userAccount);
        if (mIsSystemAdministratorSelected)
        {
            mFormModel.AccountKind = EUserAccountKind.SystemAdministrator;
            mFormModel.TeamId = getAdministrationTeamIdOrNull();
            return;
        }

        mFormModel.AccountKind = EUserAccountKind.OrganizationMember;
        ensureDefaultTeamSelection();
    }

    private void syncFormAccountKindAndTeam()
    {
        if (mCanManageSystemAdmins == false)
        {
            mIsSystemAdministratorSelected = false;
        }

        if (mIsSystemAdministratorSelected)
        {
            mFormModel.AccountKind = EUserAccountKind.SystemAdministrator;
            mFormModel.TeamId = getAdministrationTeamIdOrNull();
            mFormModel.PositionId = null;
            return;
        }

        ensureDefaultTeamSelection();
        ensureDefaultPositionSelection();
        mFormModel.AccountKind = EUserAccountKind.OrganizationMember;
    }

    private void ensureDefaultTeamSelection()
    {
        if (mIsSystemAdministratorSelected)
        {
            return;
        }

        bool isSelectedTeamAvailable = mFormModel.TeamId.HasValue && mTeams.Any(team => team.Id == mFormModel.TeamId.Value);
        if (isSelectedTeamAvailable)
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
        if (mIsSystemAdministratorSelected)
        {
            mFormModel.PositionId = null;
            return;
        }

        IReadOnlyList<OrganizationPosition> selectablePositions = getSelectablePositions();
        bool isSelectedPositionAvailable = mFormModel.PositionId.HasValue && selectablePositions.Any(position => position.Id == mFormModel.PositionId.Value);
        if (isSelectedPositionAvailable)
        {
            return;
        }

        OrganizationPosition? firstPositionOrNull = selectablePositions.FirstOrDefault();
        mFormModel.PositionId = firstPositionOrNull == null ? null : firstPositionOrNull.Id;
    }
}
