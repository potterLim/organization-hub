using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace OrganizationHub.Web.Services;

public sealed class BrowserClientTimeZoneService : IClientTimeZoneService, IAsyncDisposable
{
    private readonly IJSRuntime mJavaScriptRuntime;
    private readonly ILogger<BrowserClientTimeZoneService> mLogger;
    private IJSObjectReference? mModuleOrNull;

    public BrowserClientTimeZoneService(
        IJSRuntime javaScriptRuntime,
        ILogger<BrowserClientTimeZoneService> logger
    )
    {
        mJavaScriptRuntime = javaScriptRuntime;
        mLogger = logger;
    }

    public async Task<TimeZoneInfo> GetClientTimeZoneAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (mModuleOrNull == null)
            {
                mModuleOrNull = await mJavaScriptRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    cancellationToken,
                    "/js/organizationHubTimeZone.js");
            }

            string timeZoneId = await mModuleOrNull.InvokeAsync<string>("getClientTimeZoneId", cancellationToken);
            return ClientTimeZoneUtility.resolveTimeZone(timeZoneId);
        }
        catch (JSDisconnectedException exception)
        {
            mLogger.LogDebug(exception, "Client time zone lookup was skipped because the browser circuit disconnected.");
            return TimeZoneInfo.Local;
        }
        catch (InvalidOperationException exception)
        {
            mLogger.LogDebug(exception, "Client time zone lookup is not available before browser interop starts.");
            return TimeZoneInfo.Local;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (mModuleOrNull == null)
        {
            return;
        }

        try
        {
            await mModuleOrNull.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
