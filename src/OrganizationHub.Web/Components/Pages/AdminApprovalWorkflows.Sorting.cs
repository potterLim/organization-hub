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

public partial class AdminApprovalWorkflows
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
        await moveWorkflowAsync(sourceItemId, targetItemId, ESortableMoveDirection.Before);
    }

    [JSInvokable("MoveSortableItemAfterFromScript")]
    public async Task MoveSortableItemAfterFromScriptAsync(string sourceItemId, string targetItemId)
    {
        await moveWorkflowAsync(sourceItemId, targetItemId, ESortableMoveDirection.After);
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

    private async Task moveWorkflowAsync(string sourceItemId, string targetItemId, ESortableMoveDirection moveDirection)
    {
        ApprovalWorkflowId sourceWorkflowId = parseApprovalWorkflowId(sourceItemId);
        ApprovalWorkflowId targetWorkflowId = parseApprovalWorkflowId(targetItemId);
        if (sourceWorkflowId.HasValue == false || targetWorkflowId.HasValue == false)
        {
            return;
        }

        List<ApprovalWorkflow> reorderedWorkflows = moveWorkflowInList(sourceWorkflowId, targetWorkflowId, moveDirection);
        if (reorderedWorkflows.Count == 0)
        {
            return;
        }

        IReadOnlyList<ApprovalWorkflowId> orderedWorkflowIds = reorderedWorkflows.Select(workflow => ApprovalWorkflowId.CreateExisting(workflow.Id)).ToList();
        ServiceResult orderResult = await ApprovalWorkflowManagementService.UpdateWorkflowOrderAsync(new ApprovalWorkflowOrderUpdateRequest(orderedWorkflowIds), CancellationToken.None);
        showOperationResult(orderResult, "결재 경로 적용 순서가 저장되었습니다.");
        await loadDataAsync();
        await InvokeAsync(StateHasChanged);
    }

    private List<ApprovalWorkflow> moveWorkflowInList(
        ApprovalWorkflowId sourceWorkflowId,
        ApprovalWorkflowId targetWorkflowId,
        ESortableMoveDirection moveDirection
    )
    {
        List<ApprovalWorkflow> reorderedWorkflows = mWorkflows.ToList();
        ApprovalWorkflow? sourceWorkflowOrNull = reorderedWorkflows.FirstOrDefault(workflow => workflow.Id == sourceWorkflowId.GetRequiredValue());
        ApprovalWorkflow? targetWorkflowOrNull = reorderedWorkflows.FirstOrDefault(workflow => workflow.Id == targetWorkflowId.GetRequiredValue());
        if (sourceWorkflowOrNull == null || targetWorkflowOrNull == null)
        {
            return new List<ApprovalWorkflow>();
        }

        reorderedWorkflows.Remove(sourceWorkflowOrNull);
        int targetIndex = reorderedWorkflows.IndexOf(targetWorkflowOrNull);
        if (targetIndex < 0)
        {
            return new List<ApprovalWorkflow>();
        }

        if (moveDirection == ESortableMoveDirection.After)
        {
            targetIndex++;
        }

        reorderedWorkflows.Insert(targetIndex, sourceWorkflowOrNull);
        mWorkflows = reorderedWorkflows;
        return reorderedWorkflows;
    }

    private ApprovalWorkflowId parseApprovalWorkflowId(string itemId)
    {
        int itemIdValue;
        bool isParsed = int.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemIdValue);
        if (isParsed == false || itemIdValue <= 0)
        {
            return ApprovalWorkflowId.Empty;
        }

        return ApprovalWorkflowId.CreateExisting(itemIdValue);
    }

    private int getNextWorkflowPriority()
    {
        if (mWorkflows.Count == 0)
        {
            return 100;
        }

        return mWorkflows.Max(workflow => workflow.Priority) + 10;
    }
}
