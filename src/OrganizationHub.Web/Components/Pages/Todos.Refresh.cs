using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Todos
{
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await initializeChangeRefreshAsync();
        }

        if (mShouldInitializeSortable == false || mIsLoadingLists || mIsLoadingTasks)
        {
            return;
        }

        mShouldInitializeSortable = false;
        if (canSortTasks() == false)
        {
            await disposeSortableAsync();
            return;
        }

        if (mTodoModuleOrNull == null)
        {
            mTodoModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubTodoBoard.js");
        }

        if (mDotNetReferenceOrNull == null)
        {
            mDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        }

        await mTodoModuleOrNull.InvokeVoidAsync("initializeTodoSortable", mTodoBoardElement, mDotNetReferenceOrNull);
    }

    public async Task CheckForExternalChangesAsync()
    {
        if (mIsLoadingLists || mIsLoadingTasks || CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedTaskListID))
        {
            return;
        }

        if (ChangeVersionService.HasChanged(mLoadedChangeSnapshot) == false)
        {
            return;
        }

        if (mIsSaving || string.IsNullOrWhiteSpace(mEditingTaskID) == false || string.IsNullOrWhiteSpace(mDeletingTaskID) == false)
        {
            mHasExternalChanges = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        await loadTasksAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void recordLoadedChangeSnapshot(ChangeVersionSnapshot? loadedSnapshotOrNull = null, EChangeVersionStability stability = EChangeVersionStability.Stable)
    {
        if (loadedSnapshotOrNull == null)
        {
            mLoadedChangeSnapshot = ChangeVersionService.GetSnapshot(getChangeVersionKeys());
        }
        else
        {
            mLoadedChangeSnapshot = loadedSnapshotOrNull;
        }

        mHasExternalChanges = stability == EChangeVersionStability.Unstable;
    }

    private IReadOnlyList<string> getChangeVersionKeys()
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.TODO);
        if (TodoTaskListReference.IsAggregateTaskListID(mSelectedTaskListID) == false)
        {
            keys.Add(ChangeVersionKeys.CreateTodoTaskListKey(mSelectedTaskListID));
        }

        return keys;
    }

    private async ValueTask disposeSortableAsync()
    {
        if (mTodoModuleOrNull == null)
        {
            return;
        }

        await mTodoModuleOrNull.InvokeVoidAsync("disposeTodoSortable", mTodoBoardElement);
    }

    private async Task initializeChangeRefreshAsync()
    {
        if (mDotNetReferenceOrNull == null)
        {
            mDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        }

        mRefreshModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubChangeRefresh.js");
        await mRefreshModuleOrNull.InvokeVoidAsync("initializeChangeRefresh", mRefreshScopeElement, mDotNetReferenceOrNull);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (mRefreshModuleOrNull != null)
            {
                await mRefreshModuleOrNull.InvokeVoidAsync("disposeChangeRefresh", mRefreshScopeElement);
                await mRefreshModuleOrNull.DisposeAsync();
            }

            if (mTodoModuleOrNull != null)
            {
                await mTodoModuleOrNull.InvokeVoidAsync("disposeTodoSortable", mTodoBoardElement);
                await mTodoModuleOrNull.DisposeAsync();
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
}
