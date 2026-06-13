using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components;

public partial class DriveFileBrowser
{
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender == false)
        {
            return;
        }

        mRefreshModuleOrNull = await JavaScriptRuntime.InvokeAsync<IJSObjectReference>("import", "/js/organizationHubChangeRefresh.js");
        mRefreshDotNetReferenceOrNull = DotNetObjectReference.Create(this);
        await mRefreshModuleOrNull.InvokeVoidAsync("initializeChangeRefresh", mRefreshScopeElement, mRefreshDotNetReferenceOrNull);
    }

    [JSInvokable("CheckForExternalChanges")]
    public async Task CheckForExternalChangesAsync()
    {
        if (mIsLoadingRoots || mIsLoadingFiles || CurrentUserSessionOrNull == null || string.IsNullOrWhiteSpace(mSelectedRootFolderID))
        {
            return;
        }

        if (ChangeVersionService.HasChanged(mLoadedChangeSnapshot) == false)
        {
            return;
        }

        await loadCurrentFolderAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void recordLoadedChangeSnapshot(ChangeVersionSnapshot? loadedSnapshotOrNull = null)
    {
        if (loadedSnapshotOrNull == null)
        {
            mLoadedChangeSnapshot = ChangeVersionService.GetSnapshot(getChangeVersionKeys());
            return;
        }

        mLoadedChangeSnapshot = loadedSnapshotOrNull;
    }

    private IReadOnlyList<string> getChangeVersionKeys()
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.CreateDriveFolderKey(getCurrentFolderID()));
        return keys;
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
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        if (mRefreshDotNetReferenceOrNull != null)
        {
            mRefreshDotNetReferenceOrNull.Dispose();
        }
    }
}
