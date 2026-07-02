using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class SortableItemMoveRequest
{
    public SortableItemKey SourceItemKey
    {
        get;
    }

    public SortableItemKey TargetItemKey
    {
        get;
    }

    public ESortableMoveDirection MoveDirection
    {
        get;
    }

    public bool HasRequiredItems
    {
        get
        {
            return SourceItemKey.HasValue && TargetItemKey.HasValue;
        }
    }

    public SortableItemMoveRequest(
        SortableItemKey sourceItemKey,
        SortableItemKey targetItemKey,
        ESortableMoveDirection moveDirection
    )
    {
        SourceItemKey = sourceItemKey;
        TargetItemKey = targetItemKey;
        MoveDirection = moveDirection;
    }

    public static SortableItemMoveRequest CreateFromScript(
        string? sourceItemKeyOrNull,
        string? targetItemKeyOrNull,
        ESortableMoveDirection moveDirection
    )
    {
        return new SortableItemMoveRequest(
            SortableItemKey.CreateOptional(sourceItemKeyOrNull),
            SortableItemKey.CreateOptional(targetItemKeyOrNull),
            moveDirection);
    }
}
