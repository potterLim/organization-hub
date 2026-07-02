using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrganizationHub.Web.Components.Pages;

public partial class Dashboard
{
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender == false)
        {
            return;
        }

        await initializeClientTimeZoneAsync();
    }

    private async Task initializeClientTimeZoneAsync()
    {
        TimeZoneInfo clientTimeZoneInfo = await ClientTimeZoneService.GetClientTimeZoneAsync(CancellationToken.None);
        if (string.Equals(mClientTimeZoneInfo.Id, clientTimeZoneInfo.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mClientTimeZoneInfo = clientTimeZoneInfo;
        await loadWeekEventsAsync();
        await loadWeekTodosAsync();
        await InvokeAsync(StateHasChanged);
    }
}
