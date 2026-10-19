using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminMenus
{
    private void editMenu(NavigationMenuItem menuItem)
    {
        mSelectedMenuItemOrNull = menuItem;
        mFormModel = new NavigationMenuItemFormModel();
        mFormModel.Id = menuItem.Id;
        mFormModel.Revision = menuItem.Revision;
        mFormModel.SectionId = menuItem.SectionId;
        mFormModel.DisplayName = menuItem.DisplayName;
        mFormModel.SortOrder = menuItem.SortOrder;
        mFormModel.AccessLevel = menuItem.AccessLevel;
        mFormModel.IsActive = menuItem.IsActive;
        mFormModel.IsVisibleToAllTeams = menuItem.IsVisibleToAllTeams;
        mFormModel.RequiredPermissionKeys = NavigationMenuPermissionKeys.ParseStorageValue(menuItem.RequiredPermissionKeys)
            .Select(permissionKey => permissionKey.GetRequiredValue())
            .ToList();
        mFormModel.TeamIds = menuItem.TeamPermissions.Select(permission => permission.TeamId).ToList();
    }

    private async Task saveMenuAsync()
    {
        int menuId = mFormModel.Id;
        ServiceResult saveResult = await NavigationMenuService.UpdateMenuItemAsync(mFormModel, CancellationToken.None);
        showOperationResult(saveResult, "메뉴 표시 설정이 저장되었습니다.");
        if (saveResult.IsSuccess)
        {
            await loadDataAsync();
            NavigationMenuItem? menuItemOrNull = mMenuItems.FirstOrDefault(menuItem => menuItem.Id == menuId);
            if (menuItemOrNull != null)
            {
                editMenu(menuItemOrNull);
            }
        }
    }

    private void editSection(NavigationMenuSection section)
    {
        mSectionFormModel = new NavigationMenuSectionFormModel();
        mSectionFormModel.Id = section.Id;
        mSectionFormModel.Revision = section.Revision;
        mSectionFormModel.DisplayName = section.DisplayName;
    }

    private void clearSectionForm()
    {
        mSectionFormModel = new NavigationMenuSectionFormModel();
    }

    private async Task saveSectionAsync()
    {
        int sectionId = mSectionFormModel.Id;
        ServiceResult saveResult = await NavigationMenuService.SaveMenuSectionAsync(mSectionFormModel, CancellationToken.None);
        showOperationResult(saveResult, "메뉴 섹션이 저장되었습니다.");
        if (saveResult.IsSuccess)
        {
            await loadDataAsync();
            NavigationMenuSection? sectionOrNull = mSections.FirstOrDefault(section => section.Id == sectionId);
            if (sectionOrNull != null)
            {
                editSection(sectionOrNull);
            }
            else
            {
                clearSectionForm();
            }

            restoreSelectedMenu();
        }
    }

    private void setVisibleToAllTeams(EMenuTeamVisibility menuTeamVisibility)
    {
        mFormModel.IsVisibleToAllTeams = menuTeamVisibility == EMenuTeamVisibility.AllTeams;
        if (menuTeamVisibility == EMenuTeamVisibility.AllTeams)
        {
            mFormModel.TeamIds.Clear();
        }
    }

    private void setAccessLevel(EMenuAccessLevel accessLevel)
    {
        mFormModel.AccessLevel = accessLevel;
        if (MenuAccessLevelUtility.IsTeamScoped(accessLevel) == false)
        {
            mFormModel.IsVisibleToAllTeams = false;
            mFormModel.TeamIds.Clear();
        }
    }

    private void setTeamSelected(int teamId, ETeamSelectionState selectionState)
    {
        if (selectionState == ETeamSelectionState.Selected)
        {
            if (mFormModel.TeamIds.Contains(teamId) == false)
            {
                mFormModel.TeamIds.Add(teamId);
            }

            return;
        }

        mFormModel.TeamIds.Remove(teamId);
    }

    private bool isTeamSelected(int teamId)
    {
        return mFormModel.TeamIds.Contains(teamId);
    }

    private void setRequiredPermissionKeys(IEnumerable<string> permissionKeys)
    {
        mFormModel.RequiredPermissionKeys = permissionKeys.ToList();
    }
}
