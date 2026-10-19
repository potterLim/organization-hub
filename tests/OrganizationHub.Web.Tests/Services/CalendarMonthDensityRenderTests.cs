using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CalendarMonthDensityRenderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(999)]
    public async Task MonthCellKeepsExactCountAndBoundsPreviewsAsync(int eventCount)
    {
        CalendarDayCell day = new CalendarDayCell();
        day.Date = new DateTime(2026, 10, 20);
        day.IsCurrentMonth = true;
        day.IsToday = true;
        for (int index = 0; index < eventCount; index++)
        {
            CalendarEventItem item = new CalendarEventItem();
            item.Id = "event-" + index;
            item.Summary = "긴 일정 제목 " + new string('A', 160);
            item.Start = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(9));
            day.Events.Add(item);
        }

        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        await using (ServiceProvider provider = services.BuildServiceProvider())
        await using (HtmlRenderer renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>()))
        {
            Dictionary<string, object?> parameters = new Dictionary<string, object?>();
            parameters[nameof(CalendarMonthGridPanel.DayCells)] = new CalendarDayCell[] { day };
            parameters[nameof(CalendarMonthGridPanel.SelectedDayOrNull)] = day.Date;
            string html = await renderer.Dispatcher.InvokeAsync(renderAsync);
            Assert.Contains("10월 20일 일정 " + eventCount + "건 보기", html, StringComparison.Ordinal);
            Assert.Equal(Math.Min(eventCount, 2), Regex.Matches(html, "class=\"hub-calendar-day-time-line\"").Count);
            Assert.Equal(eventCount, day.Events.Count);

            async Task<string> renderAsync()
            {
                Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent component = await renderer.RenderComponentAsync<CalendarMonthGridPanel>(ParameterView.FromDictionary(parameters));
                return System.Net.WebUtility.HtmlDecode(component.ToHtmlString());
            }
        }
    }
}
