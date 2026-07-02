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

public partial class AdminPositions
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
        await movePositionAsync(SortableItemMoveRequest.CreateFromScript(sourceItemId, targetItemId, ESortableMoveDirection.Before));
    }

    [JSInvokable("MoveSortableItemAfterFromScript")]
    public async Task MoveSortableItemAfterFromScriptAsync(string sourceItemId, string targetItemId)
    {
        await movePositionAsync(SortableItemMoveRequest.CreateFromScript(sourceItemId, targetItemId, ESortableMoveDirection.After));
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

    private async Task movePositionAsync(SortableItemMoveRequest moveRequest)
    {
        if (moveRequest.HasRequiredItems == false)
        {
            return;
        }

        OrganizationPositionId sourcePositionId = parseOrganizationPositionId(moveRequest.SourceItemKey);
        OrganizationPositionId targetPositionId = parseOrganizationPositionId(moveRequest.TargetItemKey);
        if (sourcePositionId.HasValue == false || targetPositionId.HasValue == false)
        {
            return;
        }

        List<OrganizationPosition> reorderedPositions = movePositionInList(sourcePositionId, targetPositionId, moveRequest.MoveDirection);
        if (reorderedPositions.Count == 0)
        {
            return;
        }

        OrganizationTeamId teamId = OrganizationTeamId.CreateExisting(reorderedPositions[0].TeamId);
        IReadOnlyList<OrganizationPositionId> orderedPositionIds = reorderedPositions.Select(position => OrganizationPositionId.CreateExisting(position.Id)).ToList();
        OrganizationPositionOrderUpdateRequest request = new OrganizationPositionOrderUpdateRequest(teamId, orderedPositionIds);
        ServiceResult orderResult = await PositionManagementService.UpdatePositionOrderAsync(request, CancellationToken.None);
        showOperationResult(orderResult, "직책 순서가 저장되었습니다.");

        int? selectedPositionIdOrNull = mSelectedPositionIdOrNull;
        await loadDataAsync();
        mSelectedPositionIdOrNull = selectedPositionIdOrNull;
        ensurePermissionTeamSelection();
        ensurePositionSelectionForSelectedTeam();
        loadSelectedPositionForm();
        await InvokeAsync(StateHasChanged);
    }

    private List<OrganizationPosition> movePositionInList(
        OrganizationPositionId sourcePositionId,
        OrganizationPositionId targetPositionId,
        ESortableMoveDirection moveDirection
    )
    {
        OrganizationPosition? sourcePositionOrNull = mPositions.FirstOrDefault(position => position.Id == sourcePositionId.GetRequiredValue());
        OrganizationPosition? targetPositionOrNull = mPositions.FirstOrDefault(position => position.Id == targetPositionId.GetRequiredValue());
        if (sourcePositionOrNull == null || targetPositionOrNull == null)
        {
            return new List<OrganizationPosition>();
        }

        if (sourcePositionOrNull.TeamId != targetPositionOrNull.TeamId)
        {
            return new List<OrganizationPosition>();
        }

        List<OrganizationPosition> reorderedPositions = getPositionsForTeam(sourcePositionOrNull.TeamId);
        reorderedPositions.Remove(sourcePositionOrNull);
        int targetIndex = reorderedPositions.IndexOf(targetPositionOrNull);
        if (targetIndex < 0)
        {
            return new List<OrganizationPosition>();
        }

        if (moveDirection == ESortableMoveDirection.After)
        {
            targetIndex++;
        }

        reorderedPositions.Insert(targetIndex, sourcePositionOrNull);
        replacePositionsForTeam(sourcePositionOrNull.TeamId, reorderedPositions);
        return reorderedPositions;
    }

    private void replacePositionsForTeam(int teamId, IReadOnlyList<OrganizationPosition> reorderedPositions)
    {
        List<OrganizationPosition> updatedPositions = mPositions.Where(position => position.TeamId != teamId).ToList();
        updatedPositions.AddRange(reorderedPositions);
        mPositions = updatedPositions
            .OrderBy(position => position.Team == null ? 0 : position.Team.SortOrder)
            .ThenBy(position => position.TeamId)
            .ThenBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .ToList();
    }

    private OrganizationPositionId parseOrganizationPositionId(SortableItemKey itemKey)
    {
        int itemIdValue;
        bool isParsed = int.TryParse(itemKey.GetOptionalValue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out itemIdValue);
        if (isParsed == false || itemIdValue <= 0)
        {
            return OrganizationPositionId.Empty;
        }

        return OrganizationPositionId.CreateExisting(itemIdValue);
    }
}
