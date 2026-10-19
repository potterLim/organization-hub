using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private void setPermissionAllowed(string permissionKey, EPermissionGrantState grantState)
    {
        setPermissionAllowed(new PermissionKey(permissionKey), grantState);
    }

    private void setPermissionScope(string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(new PermissionKey(permissionKey));
        grantForm.ScopeType = scopeType;
        if (scopeType != EPermissionScopeType.SelectedTeam)
        {
            grantForm.TargetTeamId = null;
        }
    }

    private void setPermissionTargetTeam(string permissionKey, int? teamIdOrNull)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(new PermissionKey(permissionKey));
        grantForm.TargetTeamId = teamIdOrNull;
    }

    private PositionFormModel createFormModelFromPosition(OrganizationPosition position)
    {
        PositionFormModel formModel = createEmptyFormModel(position.TeamId);
        formModel.Id = position.Id;
        formModel.Revision = position.Revision;
        formModel.TeamId = position.TeamId;
        formModel.Name = position.Name;
        formModel.Description = position.Description;
        formModel.SortOrder = position.SortOrder;
        formModel.IsActive = position.IsActive;
        formModel.IsSystemAdministrator = position.IsSystemAdministrator;
        foreach (PositionPermissionGrant permissionGrant in position.PermissionGrants)
        {
            PositionPermissionGrantFormModel grantForm = ensureGrantForm(formModel, new PermissionKey(permissionGrant.PermissionKey));
            grantForm.IsAllowed = permissionGrant.IsAllowed;
            grantForm.ScopeType = permissionGrant.ScopeType;
            grantForm.TargetTeamId = permissionGrant.TargetTeamId;
        }

        return formModel;
    }

    private PositionFormModel createEmptyFormModel(int teamId)
    {
        PositionFormModel formModel = new PositionFormModel();
        formModel.IsActive = true;
        formModel.SortOrder = getNextPositionSortOrder(teamId);
        formModel.TeamId = teamId;

        foreach (PermissionDefinition definition in mPermissionDefinitions)
        {
            PositionPermissionGrantFormModel grantForm = new PositionPermissionGrantFormModel();
            grantForm.PermissionKey = definition.Key;
            grantForm.IsAllowed = false;
            grantForm.ScopeType = EPermissionScopeType.OwnTeam;
            formModel.PermissionGrants.Add(grantForm);
        }

        return formModel;
    }

    private PositionPermissionGrantFormModel getPermissionGrantForm(string permissionKey)
    {
        return getPermissionGrantForm(new PermissionKey(permissionKey));
    }

    private PositionPermissionGrantFormModel getPermissionGrantForm(PermissionKey permissionKey)
    {
        return ensureGrantForm(mPermissionFormModel, permissionKey);
    }

    private void setPermissionAllowed(PermissionKey permissionKey, EPermissionGrantState grantState)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(permissionKey);
        grantForm.IsAllowed = grantState == EPermissionGrantState.Allowed;
        if (grantState == EPermissionGrantState.Denied)
        {
            grantForm.TargetTeamId = null;
        }
    }

    private PositionPermissionGrantFormModel ensureGrantForm(PositionFormModel formModel, PermissionKey permissionKey)
    {
        PositionPermissionGrantFormModel? grantFormOrNull = formModel.PermissionGrants.FirstOrDefault(grantForm => grantForm.PermissionKey == permissionKey.GetRequiredValue());
        if (grantFormOrNull != null)
        {
            return grantFormOrNull;
        }

        PositionPermissionGrantFormModel createdGrantForm = new PositionPermissionGrantFormModel();
        createdGrantForm.PermissionKey = permissionKey.GetRequiredValue();
        createdGrantForm.ScopeType = EPermissionScopeType.OwnTeam;
        formModel.PermissionGrants.Add(createdGrantForm);
        return createdGrantForm;
    }
}
