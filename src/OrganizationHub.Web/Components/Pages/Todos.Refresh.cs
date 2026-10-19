using System;
using System.Collections.Generic;
using System.Threading;
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
            await initializeClientTimeZoneAsync();
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
        if (mIsLoadingLists || mIsLoadingTasks || CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedTaskListId))
        {
            return;
        }

        if (ChangeVersionService.HasChanged(mLoadedChangeSnapshot) == false)
        {
            return;
        }

        if (mIsSaving || string.IsNullOrWhiteSpace(mEditingTaskId) == false || string.IsNullOrWhiteSpace(mDeletingTaskId) == false)
        {
            mHasExternalChanges = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        await loadTasksAsync();
        await InvokeAsync(StateHasChanged);
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

    private void recordLoadedChangeSnapshot(
        ChangeVersionSnapshot? loadedSnapshotOrNull = null,
        EChangeVersionStability stability = EChangeVersionStability.Stable
    )
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

    private IReadOnlyList<ChangeVersionKey> getChangeVersionKeys()
    {
        List<ChangeVersionKey> changeVersionKeys = new List<ChangeVersionKey>();
        changeVersionKeys.Add(ChangeVersionKeys.TASKS);
        TodoTaskListId selectedTaskListId = TodoTaskListId.CreateOptional(mSelectedTaskListId);
        if (selectedTaskListId.IsAggregate == false)
        {
            changeVersionKeys.Add(ChangeVersionKeys.CreateTodoTaskListKey(selectedTaskListId));
        }

        return changeVersionKeys;
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

    private async Task initializeClientTimeZoneAsync()
    {
        TimeZoneInfo clientTimeZoneInfo = await ClientTimeZoneService.GetClientTimeZoneAsync(CancellationToken.None);
        if (string.Equals(mClientTimeZoneInfo.Id, clientTimeZoneInfo.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mClientTimeZoneInfo = clientTimeZoneInfo;
        await InvokeAsync(StateHasChanged);
    }
}
