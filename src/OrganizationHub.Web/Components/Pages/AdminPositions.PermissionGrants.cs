using System.Linq;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private void setPermissionAllowed(string permissionKey, bool isAllowed)
    {
        EPermissionGrantState grantState = isAllowed ? EPermissionGrantState.Allowed : EPermissionGrantState.Denied;
        setPermissionAllowed(new PermissionKey(permissionKey), grantState);
    }

    private void setPermissionScope(string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(new PermissionKey(permissionKey));
        grantForm.ScopeType = scopeType;
        if (scopeType != EPermissionScopeType.SelectedTeam)
        {
            grantForm.TargetTeamID = null;
        }
    }

    private void setPermissionTargetTeam(string permissionKey, int? teamIDOrNull)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(new PermissionKey(permissionKey));
        grantForm.TargetTeamID = teamIDOrNull;
    }

    private PositionFormModel createFormModelFromPosition(OrganizationPosition position)
    {
        PositionFormModel formModel = createEmptyFormModel(position.TeamID);
        formModel.ID = position.ID;
        formModel.TeamID = position.TeamID;
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
            grantForm.TargetTeamID = permissionGrant.TargetTeamID;
        }

        return formModel;
    }

    private PositionFormModel createEmptyFormModel(int teamID)
    {
        PositionFormModel formModel = new PositionFormModel();
        formModel.IsActive = true;
        formModel.SortOrder = 100;
        formModel.TeamID = teamID;

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
            grantForm.TargetTeamID = null;
        }
    }

    private PositionPermissionGrantFormModel ensureGrantForm(PositionFormModel formModel, PermissionKey permissionKey)
    {
        PositionPermissionGrantFormModel? grantFormOrNull = formModel.PermissionGrants.FirstOrDefault(grantForm => grantForm.PermissionKey == permissionKey.Value);
        if (grantFormOrNull != null)
        {
            return grantFormOrNull;
        }

        PositionPermissionGrantFormModel createdGrantForm = new PositionPermissionGrantFormModel();
        createdGrantForm.PermissionKey = permissionKey.Value;
        createdGrantForm.ScopeType = EPermissionScopeType.OwnTeam;
        formModel.PermissionGrants.Add(createdGrantForm);
        return createdGrantForm;
    }
}
