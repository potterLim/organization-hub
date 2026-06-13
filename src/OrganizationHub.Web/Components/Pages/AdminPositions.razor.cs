using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private const string ALL_PERMISSION_CATEGORY = "전체";

    private List<Team> mActiveTeams = new List<Team>();
    private List<OrganizationPosition> mPositions = new List<OrganizationPosition>();
    private List<PermissionDefinition> mPermissionDefinitions = new List<PermissionDefinition>();
    private PositionFormModel mNewPositionFormModel = new PositionFormModel();
    private PositionFormModel mPermissionFormModel = new PositionFormModel();
    private int mSelectedPermissionTeamID;
    private int? mSelectedPositionIDOrNull;
    private string mSelectedPermissionCategory = string.Empty;
    private bool mShowOnlyAllowedPermissions;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadDataAsync();
        ensurePermissionCategorySelection();
        mSelectedPermissionTeamID = getDefaultTeamID();
        mNewPositionFormModel = createEmptyFormModel(mSelectedPermissionTeamID);
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
    }

    private async Task loadDataAsync()
    {
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        IReadOnlyList<OrganizationPosition> positions = await PositionManagementService.GetPositionsAsync(CancellationToken.None);
        IReadOnlyList<PermissionDefinition> permissionDefinitions = await PositionManagementService.GetPermissionDefinitionsAsync(CancellationToken.None);
        mActiveTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
        mPositions = positions.ToList();
        mPermissionDefinitions = permissionDefinitions.OrderBy(definition => definition.Category).ThenBy(definition => definition.DisplayName).ToList();
    }

    private async Task createPositionAsync()
    {
        int createdTeamID = mNewPositionFormModel.TeamID;
        string createdPositionName = mNewPositionFormModel.Name.Trim();
        ServiceResult result = await PositionManagementService.CreatePositionAsync(mNewPositionFormModel, CancellationToken.None);
        showResult(result, "직책이 만들어졌습니다. 오른쪽에서 권한을 설정할 수 있습니다.");
        if (result.IsSuccess == false)
        {
            return;
        }

        await loadDataAsync();
        mSelectedPermissionTeamID = createdTeamID;
        OrganizationPosition? createdPositionOrNull = mPositions.FirstOrDefault(position => position.TeamID == createdTeamID && position.Name == createdPositionName);
        mSelectedPositionIDOrNull = createdPositionOrNull?.ID;
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
        mNewPositionFormModel = createEmptyFormModel(createdTeamID);
    }

    private async Task savePermissionAsync()
    {
        if (mSelectedPositionIDOrNull.HasValue == false)
        {
            mMessage = "권한을 설정할 직책을 선택해 주세요.";
            mMessageSeverity = Severity.Warning;
            return;
        }

        int selectedPositionID = mSelectedPositionIDOrNull.Value;
        ServiceResult result = await PositionManagementService.UpdatePositionAsync(mPermissionFormModel, CancellationToken.None);
        showResult(result, "직책 권한이 저장되었습니다.");
        if (result.IsSuccess == false)
        {
            return;
        }

        await loadDataAsync();
        mSelectedPositionIDOrNull = selectedPositionID;
        ensurePermissionTeamSelection();
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
    }

    private async Task togglePositionActiveAsync(OrganizationPosition position)
    {
        ServiceResult result = position.IsActive ? await PositionManagementService.DeactivatePositionAsync(position.ID, CancellationToken.None) : await PositionManagementService.ActivatePositionAsync(position.ID, CancellationToken.None);
        showResult(result, "직책 상태가 변경되었습니다.");
        await loadDataAsync();
        if (mSelectedPositionIDOrNull == position.ID)
        {
            loadSelectedPositionForm();
        }
    }

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

    private void setPermissionAllowed(string permissionKey, bool isAllowed)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(permissionKey);
        grantForm.IsAllowed = isAllowed;
        if (isAllowed == false)
        {
            grantForm.TargetTeamID = null;
        }
    }

    private void setPermissionScope(string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(permissionKey);
        grantForm.ScopeType = scopeType;
        if (scopeType != EPermissionScopeType.SelectedTeam)
        {
            grantForm.TargetTeamID = null;
        }
    }

    private void setPermissionTargetTeam(string permissionKey, int? teamIDOrNull)
    {
        PositionPermissionGrantFormModel grantForm = getPermissionGrantForm(permissionKey);
        grantForm.TargetTeamID = teamIDOrNull;
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
            PositionPermissionGrantFormModel grantForm = ensureGrantForm(formModel, permissionGrant.PermissionKey);
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
        return ensureGrantForm(mPermissionFormModel, permissionKey);
    }

    private PositionPermissionGrantFormModel ensureGrantForm(PositionFormModel formModel, string permissionKey)
    {
        PositionPermissionGrantFormModel? grantFormOrNull = formModel.PermissionGrants.FirstOrDefault(grantForm => grantForm.PermissionKey == permissionKey);
        if (grantFormOrNull != null)
        {
            return grantFormOrNull;
        }

        PositionPermissionGrantFormModel createdGrantForm = new PositionPermissionGrantFormModel();
        createdGrantForm.PermissionKey = permissionKey;
        createdGrantForm.ScopeType = EPermissionScopeType.OwnTeam;
        formModel.PermissionGrants.Add(createdGrantForm);
        return createdGrantForm;
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

    private string getAllowedPermissionCountText()
    {
        if (getSelectedPositionOrNull() == null)
        {
            return "직책 선택 필요";
        }

        return mPermissionFormModel.PermissionGrants.Count(grant => grant.IsAllowed).ToString(CultureInfo.InvariantCulture) + "개 허용";
    }

    private IReadOnlyList<string> getPermissionCategories()
    {
        return mPermissionDefinitions.Select(definition => definition.Category).Distinct().OrderBy(category => category).ToList();
    }

    private IReadOnlyList<string> getVisiblePermissionCategories()
    {
        List<string> categories = new List<string>();
        foreach (string category in getPermissionCategories())
        {
            IReadOnlyList<PermissionDefinition> visibleDefinitions = getVisiblePermissionDefinitions(category);
            if (visibleDefinitions.Count > 0)
            {
                categories.Add(category);
            }
        }

        return categories;
    }

    private void ensurePermissionCategorySelection()
    {
        IReadOnlyList<string> categories = getPermissionCategories();
        if (categories.Count == 0)
        {
            mSelectedPermissionCategory = ALL_PERMISSION_CATEGORY;
            return;
        }

        if (string.IsNullOrWhiteSpace(mSelectedPermissionCategory) || (mSelectedPermissionCategory != ALL_PERMISSION_CATEGORY && categories.Contains(mSelectedPermissionCategory) == false))
        {
            mSelectedPermissionCategory = categories[0];
        }
    }

    private IReadOnlyList<PermissionDefinition> getVisiblePermissionDefinitions(string category)
    {
        IEnumerable<PermissionDefinition> definitions = mPermissionDefinitions.Where(definition => definition.Category == category);
        if (mSelectedPermissionCategory != ALL_PERMISSION_CATEGORY)
        {
            definitions = definitions.Where(definition => definition.Category == mSelectedPermissionCategory);
        }

        if (mShowOnlyAllowedPermissions)
        {
            definitions = definitions.Where(definition => getPermissionGrantForm(definition.Key).IsAllowed);
        }

        return definitions.OrderBy(definition => definition.DisplayName).ToList();
    }

    private string getPositionOptionText(OrganizationPosition position)
    {
        if (position.IsActive)
        {
            return position.Name;
        }

        return position.Name + " (비활성)";
    }

    private string getTeamName(OrganizationPosition position)
    {
        if (position.Team == null)
        {
            return "-";
        }

        return position.Team.DisplayName;
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
}
