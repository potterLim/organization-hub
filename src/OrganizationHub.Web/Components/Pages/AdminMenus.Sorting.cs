using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminMenus
{
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
}
