using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private void setNewPositionTeamID(int teamID)
    {
        mNewPositionFormModel.TeamID = teamID;
    }

    private void setPermissionTeamID(int teamID)
    {
        mSelectedPermissionTeamID = teamID;
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
    }

    private void setSelectedPositionID(int? positionIDOrNull)
    {
        mSelectedPositionIDOrNull = positionIDOrNull;
        loadSelectedPositionForm();
    }

    private void selectPositionForPermissions(OrganizationPosition position)
    {
        mSelectedPermissionTeamID = position.TeamID;
        mSelectedPositionIDOrNull = position.ID;
        loadSelectedPositionForm();
    }

    private void ensurePermissionTeamSelection()
    {
        if (mActiveTeams.Count == 0)
        {
            mSelectedPermissionTeamID = 0;
            return;
        }

        bool hasSelectedTeam = mActiveTeams.Any(team => team.ID == mSelectedPermissionTeamID);
        if (hasSelectedTeam == false)
        {
            mSelectedPermissionTeamID = mActiveTeams[0].ID;
        }

        bool hasNewPositionTeam = mActiveTeams.Any(team => team.ID == mNewPositionFormModel.TeamID);
        if (hasNewPositionTeam == false)
        {
            mNewPositionFormModel.TeamID = mSelectedPermissionTeamID;
        }
    }

    private void ensurePositionSelectionForSelectedTeam()
    {
        ensurePermissionTeamSelection();
        IReadOnlyList<OrganizationPosition> positions = getPositionsForSelectedPermissionTeam();
        bool hasSelectedPosition = mSelectedPositionIDOrNull.HasValue && positions.Any(position => position.ID == mSelectedPositionIDOrNull.Value);
        if (hasSelectedPosition == false)
        {
            mSelectedPositionIDOrNull = positions.Count == 0 ? null : positions[0].ID;
        }
    }

    private void loadSelectedPositionForm()
    {
        OrganizationPosition? selectedPositionOrNull = getSelectedPositionOrNull();
        if (selectedPositionOrNull == null)
        {
            mPermissionFormModel = createEmptyFormModel(mSelectedPermissionTeamID);
            return;
        }

        mPermissionFormModel = createFormModelFromPosition(selectedPositionOrNull);
    }

    private int getDefaultTeamID()
    {
        if (mActiveTeams.Count == 0)
        {
            return 0;
        }

        return mActiveTeams[0].ID;
    }

    private List<OrganizationPosition> getPositionsForSelectedPermissionTeam()
    {
        return mPositions
            .Where(position => position.TeamID == mSelectedPermissionTeamID)
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .ToList();
    }

    private OrganizationPosition? getSelectedPositionOrNull()
    {
        if (mSelectedPositionIDOrNull.HasValue == false)
        {
            return null;
        }

        return mPositions.FirstOrDefault(position => position.ID == mSelectedPositionIDOrNull.Value);
    }
}
