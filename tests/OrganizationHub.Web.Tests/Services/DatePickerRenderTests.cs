using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DatePickerRenderTests
{
    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 1)]
    public async Task PickerUsesFullDateLabelsAndOneTabStopPerSelectionGroupAsync(bool includesTime, int tabStopCount)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, StaticJavaScriptRuntime>();
        services.AddMudServices();
        await using (ServiceProvider provider = services.BuildServiceProvider())
        await using (HtmlRenderer renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>()))
        {
            Dictionary<string, object?> parameters = new Dictionary<string, object?>();
            parameters[nameof(CalendarDateTimeField.IsOpen)] = true;
            parameters[nameof(CalendarDateTimeField.IsTimeSelectionEnabled)] = includesTime;
            parameters[nameof(CalendarDateTimeField.Value)] = new DateTime(2026, 10, 19, 9, 15, 0);
            parameters[nameof(CalendarDateTimeField.Label)] = "마감 기한";
            parameters[nameof(CalendarDateTimeField.CurrentDateOrNull)] = new DateTime(2026, 10, 19);
            string html = await renderer.Dispatcher.InvokeAsync(renderAsync);
            Assert.Contains("aria-label=\"2026년 10월 19일 월요일\"", html, StringComparison.Ordinal);
            Assert.Contains("aria-current=\"date\"", html, StringComparison.Ordinal);
            Assert.Equal(tabStopCount, Regex.Matches(html, "tabindex=\"0\"").Count);
            Assert.Contains("data-picker-date=\"2026-10-19\"", html, StringComparison.Ordinal);

            async Task<string> renderAsync()
            {
                await renderer.RenderComponentAsync<MudBlazor.MudPopoverProvider>(ParameterView.Empty);
                Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent component = await renderer.RenderComponentAsync<CalendarDateTimeField>(ParameterView.FromDictionary(parameters));
                return System.Net.WebUtility.HtmlDecode(component.ToHtmlString());
            }
        }
    }
}
