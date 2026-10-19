using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CalendarListPermissionRenderTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RenderedListActionsMatchIndependentPermissionsAsync(bool canEdit, bool canDelete)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, StaticJavaScriptRuntime>();
        services.AddMudServices();
        await using (ServiceProvider provider = services.BuildServiceProvider())
        await using (HtmlRenderer renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>()))
        {
            CalendarEventItem item = new CalendarEventItem();
            item.Id = "item";
            item.CanEdit = canEdit;
            item.CanDelete = canDelete;
            Dictionary<string, object?> parameters = new Dictionary<string, object?>();
            parameters[nameof(CalendarEventListPanel.MonthEvents)] = new CalendarEventItem[] { item };
            string html = await renderer.Dispatcher.InvokeAsync(renderAsync);
            Assert.Equal(canEdit, html.Contains("aria-label=\"일정 수정\"", StringComparison.Ordinal));
            Assert.Equal(canDelete, html.Contains("aria-label=\"일정 삭제\"", StringComparison.Ordinal));

            async Task<string> renderAsync()
            {
                await renderer.RenderComponentAsync<MudBlazor.MudPopoverProvider>(ParameterView.Empty);
                Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent component = await renderer.RenderComponentAsync<CalendarEventListPanel>(ParameterView.FromDictionary(parameters));
                return System.Net.WebUtility.HtmlDecode(component.ToHtmlString());
            }
        }
    }
}
