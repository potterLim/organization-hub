using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private const int INITIAL_POSITION_SORT_ORDER = 100;
    private const int NO_SELECTED_TEAM_ID = 0;
    private const int POSITION_SORT_ORDER_STEP = 10;

    private void setNewPositionTeamId(int teamId)
    {
        mNewPositionFormModel.TeamId = teamId;
        mNewPositionFormModel.SortOrder = getNextPositionSortOrder(teamId);
    }

    private void setPermissionTeamId(int teamId)
    {
        mSelectedPermissionTeamId = teamId;
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
    }

    private void setSelectedPositionId(int? positionIdOrNull)
    {
        mSelectedPositionIdOrNull = positionIdOrNull;
        loadSelectedPositionForm();
    }

    private void selectPositionForPermissions(OrganizationPosition position)
    {
        mSelectedPermissionTeamId = position.TeamId;
        mSelectedPositionIdOrNull = position.Id;
        loadSelectedPositionForm();
    }

    private void ensurePermissionTeamSelection()
    {
        if (mActiveTeams.Count == 0)
        {
            mSelectedPermissionTeamId = NO_SELECTED_TEAM_ID;
            return;
        }

        bool hasSelectedTeam = mActiveTeams.Any(team => team.Id == mSelectedPermissionTeamId);
        if (hasSelectedTeam == false)
        {
            mSelectedPermissionTeamId = mActiveTeams[0].Id;
        }

        bool hasNewPositionTeam = mActiveTeams.Any(team => team.Id == mNewPositionFormModel.TeamId);
        if (hasNewPositionTeam == false)
        {
            mNewPositionFormModel.TeamId = mSelectedPermissionTeamId;
        }
    }

    private void ensurePositionSelectionForSelectedTeam()
    {
        ensurePermissionTeamSelection();
        IReadOnlyList<OrganizationPosition> positions = getPositionsForSelectedPermissionTeam();
        bool hasSelectedPosition = mSelectedPositionIdOrNull.HasValue && positions.Any(position => position.Id == mSelectedPositionIdOrNull.Value);
        if (hasSelectedPosition == false)
        {
            mSelectedPositionIdOrNull = positions.Count == 0 ? null : positions[0].Id;
        }
    }

    private void loadSelectedPositionForm()
    {
        OrganizationPosition? selectedPositionOrNull = getSelectedPositionOrNull();
        if (selectedPositionOrNull == null)
        {
            mPermissionFormModel = createEmptyFormModel(mSelectedPermissionTeamId);
            return;
        }

        mPermissionFormModel = createFormModelFromPosition(selectedPositionOrNull);
    }

    private int getDefaultTeamId()
    {
        if (mActiveTeams.Count == 0)
        {
            return NO_SELECTED_TEAM_ID;
        }

        return mActiveTeams[0].Id;
    }

    private List<OrganizationPosition> getPositionsForSelectedPermissionTeam()
    {
        return getPositionsForTeam(mSelectedPermissionTeamId);
    }

    private List<OrganizationPosition> getPositionsForTeam(int teamId)
    {
        return mPositions
            .Where(position => position.TeamId == teamId)
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .ToList();
    }

    private int getNextPositionSortOrder(int teamId)
    {
        List<OrganizationPosition> positions = getPositionsForTeam(teamId);
        if (positions.Count == 0)
        {
            return INITIAL_POSITION_SORT_ORDER;
        }

        return positions.Max(position => position.SortOrder) + POSITION_SORT_ORDER_STEP;
    }

    private OrganizationPosition? getSelectedPositionOrNull()
    {
        if (mSelectedPositionIdOrNull.HasValue == false)
        {
            return null;
        }

        return mPositions.FirstOrDefault(position => position.Id == mSelectedPositionIdOrNull.Value);
    }
}
