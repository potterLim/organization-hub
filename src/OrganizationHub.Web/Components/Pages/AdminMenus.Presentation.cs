using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminMenus
{
    private IReadOnlyList<NavigationMenuItem> getMenuItemsForSection(NavigationMenuSection section)
    {
        return mMenuItems
            .Where(menuItem => menuItem.SectionId == section.Id)
            .OrderBy(menuItem => menuItem.SortOrder)
            .ThenBy(menuItem => menuItem.DisplayName)
            .ToList();
    }

    private string getMenuSortableItemId(NavigationMenuItem menuItem)
    {
        return MENU_SORTABLE_ITEM_PREFIX + menuItem.Id.ToString(CultureInfo.InvariantCulture);
    }

    private string getSectionSortableItemId(NavigationMenuSection section)
    {
        return SECTION_SORTABLE_ITEM_PREFIX + section.Id.ToString(CultureInfo.InvariantCulture);
    }

    private string getMenuSortableRowClass(NavigationMenuItem menuItem)
    {
        if (isSelectedMenu(menuItem))
        {
            return "hub-sortable-row hub-menu-sortable-row hub-menu-sortable-row-selected";
        }

        return "hub-sortable-row hub-menu-sortable-row";
    }

    private bool isSelectedMenu(NavigationMenuItem menuItem)
    {
        return mSelectedMenuItemOrNull != null && mSelectedMenuItemOrNull.Id == menuItem.Id;
    }

    private string getSectionMenuCountText(NavigationMenuSection section)
    {
        int menuCount = mMenuItems.Count(menuItem => menuItem.SectionId == section.Id);
        if (menuCount == 0)
        {
            return "메뉴 없음";
        }

        return "메뉴 " + menuCount.ToString(CultureInfo.InvariantCulture) + "개";
    }

    private string getTeamPermissionText(NavigationMenuItem menuItem)
    {
        if (MenuAccessLevelUtility.IsTeamScoped(menuItem.AccessLevel) == false)
        {
            return "팀 제한 없음";
        }

        if (menuItem.IsVisibleToAllTeams)
        {
            return "모든 팀";
        }

        List<string> teamNames = new List<string>();
        foreach (NavigationMenuTeamPermission permission in menuItem.TeamPermissions)
        {
            if (permission.Team != null && permission.Team.IsActive)
            {
                teamNames.Add(permission.Team.DisplayName);
            }
        }

        if (teamNames.Count == 0)
        {
            return "팀 미선택";
        }

        return string.Join(", ", teamNames);
    }

    private string getRequiredPermissionText()
    {
        return createRequiredPermissionText(mFormModel.RequiredPermissionKeyValues);
    }

    private string getRequiredPermissionSelectionText(IReadOnlyList<string> permissionKeys)
    {
        IReadOnlyList<PermissionKey> permissionKeyValues = NavigationMenuPermissionKeys.CreatePermissionKeys(permissionKeys);
        List<string> permissionNames = createPermissionNames(permissionKeyValues);
        if (permissionNames.Count == 0)
        {
            return "로그인한 사용자";
        }

        if (permissionNames.Count == 1)
        {
            return permissionNames[0];
        }

        return permissionNames[0] + " 외 " + (permissionNames.Count - 1).ToString(CultureInfo.InvariantCulture) + "개";
    }

    private string createRequiredPermissionText(IReadOnlyList<PermissionKey> permissionKeys)
    {
        List<string> permissionNames = createPermissionNames(permissionKeys);
        if (permissionNames.Count == 0)
        {
            return "로그인한 사용자";
        }

        return string.Join(" 또는 ", permissionNames);
    }

    private List<string> createPermissionNames(IReadOnlyList<PermissionKey> permissionKeys)
    {
        Dictionary<string, PermissionDefinition> definitionsByKey = mPermissionDefinitions.ToDictionary(definition => definition.Key);
        List<string> permissionNames = new List<string>();
        foreach (PermissionKey permissionKey in permissionKeys)
        {
            PermissionDefinition? definitionOrNull;
            if (definitionsByKey.TryGetValue(permissionKey.GetRequiredValue(), out definitionOrNull))
            {
                permissionNames.Add(definitionOrNull.Category + " · " + definitionOrNull.DisplayName);
                continue;
            }

            permissionNames.Add("등록되지 않은 권한");
        }

        return permissionNames.Distinct().ToList();
    }

    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
        mHasSaveConflict = operationResult.IsConcurrencyConflict;
        if (operationResult.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = operationResult.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }
}
