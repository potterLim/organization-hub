using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class SortableOrderControlTests
{
    [Theory]
    [InlineData("menu:")]
    [InlineData("section:")]
    public async Task MenuKeyboardMovesPreserveSortableKeyNamespaceAsync(string prefix)
    {
        List<SortableItemMoveRequest> requests = new List<SortableItemMoveRequest>();
        SortableOrderControls control = createControl(2, requests);
        typeof(SortableOrderControls).GetProperty("ItemKeyPrefix")!.SetValue(control, prefix);
        await invokeMoveAsync(control, -1);
        SortableItemMoveRequest request = Assert.Single(requests);
        Assert.Equal(prefix + "2", request.SourceItemKey.GetRequiredValue());
        Assert.Equal(prefix + "1", request.TargetItemKey.GetRequiredValue());
    }

    [Theory]
    [InlineData(2, -1, 1, ESortableMoveDirection.Before)]
    [InlineData(2, 1, 3, ESortableMoveDirection.After)]
    public async Task KeyboardMoveUsesAdjacentItemAndExistingReorderContractAsync(int itemId, int offset, int targetId, ESortableMoveDirection direction)
    {
        List<SortableItemMoveRequest> requests = new List<SortableItemMoveRequest>();
        SortableOrderControls control = createControl(itemId, requests);
        await invokeMoveAsync(control, offset);
        SortableItemMoveRequest request = Assert.Single(requests);
        Assert.Equal(SortableItemKey.CreateOptional(itemId.ToString()), request.SourceItemKey);
        Assert.Equal(SortableItemKey.CreateOptional(targetId.ToString()), request.TargetItemKey);
        Assert.Equal(direction, request.MoveDirection);
    }

    [Theory]
    [InlineData(1, -1)]
    [InlineData(3, 1)]
    [InlineData(99, 1)]
    public async Task BoundaryAndMissingItemsDoNotReorderAsync(int itemId, int offset)
    {
        List<SortableItemMoveRequest> requests = new List<SortableItemMoveRequest>();
        await invokeMoveAsync(createControl(itemId, requests), offset);
        Assert.Empty(requests);
    }

    private static SortableOrderControls createControl(int itemId, List<SortableItemMoveRequest> requests)
    {
        SortableOrderControls control = new SortableOrderControls();
        typeof(SortableOrderControls).GetProperty("ItemId")!.SetValue(control, itemId);
        typeof(SortableOrderControls).GetProperty("OrderedIds")!.SetValue(control, new int[] { 1, 2, 3 });
        EventCallback<SortableItemMoveRequest> callback = EventCallback.Factory.Create<SortableItemMoveRequest>(requests, (SortableItemMoveRequest request) => requests.Add(request));
        typeof(SortableOrderControls).GetProperty("MoveRequested")!.SetValue(control, callback);
        return control;
    }

    private static Task invokeMoveAsync(SortableOrderControls control, int offset)
    {
        return (Task)typeof(SortableOrderControls).GetMethod("moveAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, new object[] { offset })!;
    }
}
