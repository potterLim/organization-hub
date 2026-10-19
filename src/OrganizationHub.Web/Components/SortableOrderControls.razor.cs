using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components;

public partial class SortableOrderControls
{
    private bool mIsMoving;

    [Parameter]
    public int ItemId
    {
        get; set;
    }
    [Parameter]
    public string Label { get; set; } = "항목";
    [Parameter]
    public IReadOnlyList<int> OrderedIds { get; set; } = Array.Empty<int>();
    [Parameter]
    public EventCallback<SortableItemMoveRequest> MoveRequested
    {
        get; set;
    }

    private int getIndex()
    {
        for (int index = 0; index < OrderedIds.Count; index++)
        {
            if (OrderedIds[index] == ItemId)
            {
                return index;
            }
        }
        return -1;
    }

    private bool canMove(int offset)
    {
        int index = getIndex();
        return mIsMoving == false && index >= 0 && index + offset >= 0 && index + offset < OrderedIds.Count;
    }

    private Task moveUpAsync()
    {
        return moveAsync(-1);
    }

    private Task moveDownAsync()
    {
        return moveAsync(1);
    }

    private async Task moveAsync(int offset)
    {
        if (canMove(offset) == false)
        {
            return;
        }
        int target = OrderedIds[getIndex() + offset];
        SortableItemMoveRequest request = SortableItemMoveRequest.CreateFromScript(ItemId.ToString(CultureInfo.InvariantCulture), target.ToString(CultureInfo.InvariantCulture), offset < 0 ? ESortableMoveDirection.Before : ESortableMoveDirection.After);
        mIsMoving = true;
        try
        {
            await MoveRequested.InvokeAsync(request);
        }
        finally
        {
            mIsMoving = false;
        }
    }
}
