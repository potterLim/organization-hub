using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components;

public abstract class TimeZoneAwareOrganizationHubComponentBase : OrganizationHubComponentBase
{
    [Inject]
    protected IClientTimeZoneService ClientTimeZoneService { get; set; } = null!;

    protected TimeZoneInfo ClientTimeZoneInfo
    {
        get; private set;
    } = TimeZoneInfo.Local;

    protected async Task initializeClientTimeZoneAsync()
    {
        TimeZoneInfo clientTimeZoneInfo = await ClientTimeZoneService.GetClientTimeZoneAsync(CancellationToken.None);
        if (string.Equals(ClientTimeZoneInfo.Id, clientTimeZoneInfo.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ClientTimeZoneInfo = clientTimeZoneInfo;
        await InvokeAsync(StateHasChanged);
    }
}
