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
    private const string MENU_SORTABLE_ITEM_PREFIX = "menu:";
    private const string SECTION_SORTABLE_ITEM_PREFIX = "section:";

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
        await moveSortableItemAsync(sourceItemId, targetItemId, ESortableMoveDirection.Before);
    }

    [JSInvokable("MoveSortableItemAfterFromScript")]
    public async Task MoveSortableItemAfterFromScriptAsync(string sourceItemId, string targetItemId)
    {
        await moveSortableItemAsync(sourceItemId, targetItemId, ESortableMoveDirection.After);
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

    private async Task moveSortableItemAsync(string sourceItemId, string targetItemId, ESortableMoveDirection moveDirection)
    {
        if (isSectionSortableItem(sourceItemId) && isSectionSortableItem(targetItemId))
        {
            await moveSectionAsync(sourceItemId, targetItemId, moveDirection);
            return;
        }

        if (isMenuSortableItem(sourceItemId) && isMenuSortableItem(targetItemId))
        {
            await moveMenuItemAsync(sourceItemId, targetItemId, moveDirection);
        }
    }

    private async Task moveSectionAsync(string sourceItemId, string targetItemId, ESortableMoveDirection moveDirection)
    {
        NavigationMenuSectionId sourceSectionId = parseNavigationMenuSectionId(sourceItemId);
        NavigationMenuSectionId targetSectionId = parseNavigationMenuSectionId(targetItemId);
        if (sourceSectionId.HasValue == false || targetSectionId.HasValue == false)
        {
            return;
        }

        List<NavigationMenuSection> reorderedSections = moveSectionInList(sourceSectionId, targetSectionId, moveDirection);
        if (reorderedSections.Count == 0)
        {
            return;
        }

        IReadOnlyList<NavigationMenuSectionId> orderedSectionIds = reorderedSections.Select(section => NavigationMenuSectionId.CreateExisting(section.Id)).ToList();
        ServiceResult orderResult = await NavigationMenuService.UpdateMenuSectionOrderAsync(new NavigationMenuSectionOrderUpdateRequest(orderedSectionIds), CancellationToken.None);
        showOperationResult(orderResult, "섹션 순서가 저장되었습니다.");
        await loadDataAsync();
        restoreSelectedMenu();
        await InvokeAsync(StateHasChanged);
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

    private List<NavigationMenuSection> moveSectionInList(
        NavigationMenuSectionId sourceSectionId,
        NavigationMenuSectionId targetSectionId,
        ESortableMoveDirection moveDirection
    )
    {
        List<NavigationMenuSection> reorderedSections = mSections.ToList();
        NavigationMenuSection? sourceSectionOrNull = reorderedSections.FirstOrDefault(section => section.Id == sourceSectionId.GetRequiredValue());
        NavigationMenuSection? targetSectionOrNull = reorderedSections.FirstOrDefault(section => section.Id == targetSectionId.GetRequiredValue());
        if (sourceSectionOrNull == null || targetSectionOrNull == null)
        {
            return new List<NavigationMenuSection>();
        }

        reorderedSections.Remove(sourceSectionOrNull);
        int targetIndex = reorderedSections.IndexOf(targetSectionOrNull);
        if (targetIndex < 0)
        {
            return new List<NavigationMenuSection>();
        }

        if (moveDirection == ESortableMoveDirection.After)
        {
            targetIndex++;
        }

        reorderedSections.Insert(targetIndex, sourceSectionOrNull);
        mSections = reorderedSections;
        return reorderedSections;
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
        string rawItemId = removeSortableItemPrefix(itemId, MENU_SORTABLE_ITEM_PREFIX);
        int itemIdValue;
        bool isParsed = int.TryParse(rawItemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemIdValue);
        if (isParsed == false || itemIdValue <= 0)
        {
            return NavigationMenuItemId.Empty;
        }

        return NavigationMenuItemId.CreateExisting(itemIdValue);
    }

    private NavigationMenuSectionId parseNavigationMenuSectionId(string itemId)
    {
        string rawItemId = removeSortableItemPrefix(itemId, SECTION_SORTABLE_ITEM_PREFIX);
        int itemIdValue;
        bool isParsed = int.TryParse(rawItemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemIdValue);
        if (isParsed == false || itemIdValue <= 0)
        {
            return NavigationMenuSectionId.Empty;
        }

        return NavigationMenuSectionId.CreateExisting(itemIdValue);
    }

    private bool isMenuSortableItem(string itemId)
    {
        return itemId.StartsWith(MENU_SORTABLE_ITEM_PREFIX, StringComparison.Ordinal);
    }

    private bool isSectionSortableItem(string itemId)
    {
        return itemId.StartsWith(SECTION_SORTABLE_ITEM_PREFIX, StringComparison.Ordinal);
    }

    private string removeSortableItemPrefix(string itemId, string prefix)
    {
        if (itemId.StartsWith(prefix, StringComparison.Ordinal) == false)
        {
            return string.Empty;
        }

        return itemId[prefix.Length..];
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
