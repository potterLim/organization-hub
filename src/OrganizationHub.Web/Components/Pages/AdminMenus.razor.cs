using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminMenus
{
    private List<NavigationMenuItem> mMenuItems = new List<NavigationMenuItem>();
    private List<Team> mTeams = new List<Team>();
    private NavigationMenuItem? mSelectedMenuItemOrNull;
    private NavigationMenuItemFormModel mFormModel = new NavigationMenuItemFormModel();
    private ElementReference mSortableRootElement;
    private IJSObjectReference? mSortableModuleOrNull;
    private DotNetObjectReference<AdminMenus>? mDotNetReferenceOrNull;
    private bool mShouldInitializeSortable = true;
    private string mMessage = string.Empty;
    private Severity mMessageSeverity = Severity.Info;

    protected override async Task OnInitializedAsync()
    {
        await loadDataAsync();
        if (mMenuItems.Count > 0)
        {
            editMenu(mMenuItems[0]);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (mShouldInitializeSortable == false)
        {
            return;
        }

        mShouldInitializeSortable = false;
        if (mSortableModuleOrNull == null)
        {
            mSortableModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubSortableList.js");
        }

        if (mDotNetReferenceOrNull == null)
        {
            mDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        }

        await mSortableModuleOrNull.InvokeVoidAsync("initializeSortableLists", mSortableRootElement, mDotNetReferenceOrNull);
    }

    [JSInvokable("MoveSortableItemBeforeFromScript")]
    public async Task MoveSortableItemBeforeFromScriptAsync(string sourceItemId, string targetItemId)
    {
        await moveMenuItemAsync(sourceItemId, targetItemId, ESortableMoveDirection.Before);
    }

    [JSInvokable("MoveSortableItemAfterFromScript")]
    public async Task MoveSortableItemAfterFromScriptAsync(string sourceItemId, string targetItemId)
    {
        await moveMenuItemAsync(sourceItemId, targetItemId, ESortableMoveDirection.After);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (mSortableModuleOrNull != null)
            {
                await mSortableModuleOrNull.InvokeVoidAsync("disposeSortableLists", mSortableRootElement);
                await mSortableModuleOrNull.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        if (mDotNetReferenceOrNull != null)
        {
            mDotNetReferenceOrNull.Dispose();
        }
    }

    private async Task loadDataAsync()
    {
        IReadOnlyList<NavigationMenuItem> menuItems = await NavigationMenuService.GetManageableMenuItemsAsync(CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        mMenuItems = menuItems.ToList();
        mTeams = teams.Where(team => team.IsActive).ToList();
        mShouldInitializeSortable = true;
    }

    private void editMenu(NavigationMenuItem menuItem)
    {
        mSelectedMenuItemOrNull = menuItem;
        mFormModel = new NavigationMenuItemFormModel();
        mFormModel.Id = menuItem.Id;
        mFormModel.DisplayName = menuItem.DisplayName;
        mFormModel.SortOrder = menuItem.SortOrder;
        mFormModel.AccessLevel = menuItem.AccessLevel;
        mFormModel.IsActive = menuItem.IsActive;
        mFormModel.IsVisibleToAllTeams = menuItem.IsVisibleToAllTeams;
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

    private async Task moveMenuItemAsync(string sourceItemId, string targetItemId, ESortableMoveDirection moveDirection)
    {
        NavigationMenuItemId sourceMenuItemId = parseNavigationMenuItemId(sourceItemId);
        NavigationMenuItemId targetMenuItemId = parseNavigationMenuItemId(targetItemId);
        if (sourceMenuItemId.HasValue == false || targetMenuItemId.HasValue == false)
        {
            return;
        }

        List<NavigationMenuItem> reorderedMenuItems = moveMenuItemInList(sourceMenuItemId, targetMenuItemId, moveDirection);
        if (reorderedMenuItems.Count == 0)
        {
            return;
        }

        IReadOnlyList<NavigationMenuItemId> orderedMenuItemIds = reorderedMenuItems.Select(menuItem => NavigationMenuItemId.CreateExisting(menuItem.Id)).ToList();
        ServiceResult orderResult = await NavigationMenuService.UpdateMenuItemOrderAsync(new NavigationMenuItemOrderUpdateRequest(orderedMenuItemIds), CancellationToken.None);
        showOperationResult(orderResult, "메뉴 순서가 저장되었습니다.");
        await loadDataAsync();
        restoreSelectedMenu();
        await InvokeAsync(StateHasChanged);
    }

    private List<NavigationMenuItem> moveMenuItemInList(
        NavigationMenuItemId sourceMenuItemId,
        NavigationMenuItemId targetMenuItemId,
        ESortableMoveDirection moveDirection
    )
    {
        List<NavigationMenuItem> reorderedMenuItems = mMenuItems.ToList();
        NavigationMenuItem? sourceMenuItemOrNull = reorderedMenuItems.FirstOrDefault(menuItem => menuItem.Id == sourceMenuItemId.GetRequiredValue());
        NavigationMenuItem? targetMenuItemOrNull = reorderedMenuItems.FirstOrDefault(menuItem => menuItem.Id == targetMenuItemId.GetRequiredValue());
        if (sourceMenuItemOrNull == null || targetMenuItemOrNull == null)
        {
            return new List<NavigationMenuItem>();
        }

        reorderedMenuItems.Remove(sourceMenuItemOrNull);
        int targetIndex = reorderedMenuItems.IndexOf(targetMenuItemOrNull);
        if (targetIndex < 0)
        {
            return new List<NavigationMenuItem>();
        }

        if (moveDirection == ESortableMoveDirection.After)
        {
            targetIndex++;
        }

        reorderedMenuItems.Insert(targetIndex, sourceMenuItemOrNull);
        mMenuItems = reorderedMenuItems;
        return reorderedMenuItems;
    }

    private NavigationMenuItemId parseNavigationMenuItemId(string itemId)
    {
        int itemIdValue;
        bool isParsed = int.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemIdValue);
        if (isParsed == false || itemIdValue <= 0)
        {
            return NavigationMenuItemId.Empty;
        }

        return NavigationMenuItemId.CreateExisting(itemIdValue);
    }

    private void restoreSelectedMenu()
    {
        if (mSelectedMenuItemOrNull == null)
        {
            return;
        }

        int selectedMenuItemId = mSelectedMenuItemOrNull.Id;
        NavigationMenuItem? selectedMenuItemOrNull = mMenuItems.FirstOrDefault(menuItem => menuItem.Id == selectedMenuItemId);
        if (selectedMenuItemOrNull != null)
        {
            editMenu(selectedMenuItemOrNull);
        }
    }

    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
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
