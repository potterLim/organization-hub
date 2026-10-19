using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
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
        await initializeClientTimeZoneAsync();
    }

    [JSInvokable("CheckForExternalChanges")]
    public async Task CheckForExternalChangesAsync()
    {
        if (mIsLoading || CurrentUserSessionOrNull == null)
        {
            return;
        }

        if (ChangeVersionService.HasChanged(mLoadedChangeSnapshot) == false)
        {
            return;
        }

        if (mIsEditorOpen || mDeleteCandidateOrNull != null)
        {
            mHasExternalChanges = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        await loadEventsAsync();
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
        changeVersionKeys.Add(ChangeVersionKeys.CALENDAR);
        if (mSelectedCalendarId != "all")
        {
            changeVersionKeys.Add(ChangeVersionKeys.CreateCalendarKey(CalendarId.CreateOptional(mSelectedCalendarId)));
        }

        return changeVersionKeys;
    }
}
