using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CalendarPeriodRegressionTests
{
    [Fact]
    public async Task ReopeningAndSavingGoogleAllDayEventKeepsExclusiveEndAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_UPDATE))
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (CalendarContractTransport transport = new CalendarContractTransport())
        {
            OrganizationPermissionService organization = new OrganizationPermissionService(factory);
            PermissionService permissions = new PermissionService(factory, new FakeDriveService(), cache, organization);
            GoogleCalendarService service = new GoogleCalendarService(transport, Options.Create(new GoogleApiOptions()), factory, cache, NullLogger<GoogleCalendarService>.Instance, new ChangeVersionService(), permissions);
            UserSession session = OperationPermissionRegressionTests.createSession();
            CalendarEventItem item = new CalendarEventItem();
            item.Id = "period";
            item.CalendarId = "calendar";
            item.ETag = "\"v1\"";
            item.Summary = "종일 행사";
            item.IsAllDay = true;
            item.CanEdit = true;
            item.Start = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(9));
            item.End = item.Start.Value.AddDays(1);
            Calendar page = new Calendar();
            for (int repetition = 0; repetition < 3; repetition++)
            {
                typeof(Calendar).GetMethod("openEditEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, new object[] { item });
                DateTime end = (DateTime)typeof(Calendar).GetField("mEditorEndLocalOrNull", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
                Assert.Equal(new DateTime(2026, 10, 19), end.Date);
                CalendarEventFormModel form = new CalendarEventFormModel();
                form.Id = item.Id;
                form.CalendarId = item.CalendarId;
                form.ETag = item.ETag;
                form.Summary = item.Summary;
                form.IsAllDay = true;
                form.StartLocal = new DateTime(2026, 10, 19);
                form.EndLocal = end;
                item = await service.UpdateEventAsync(session, form, CalendarName.CreateOptional("일정"), CancellationToken.None);
                Assert.Equal("2026-10-20", transport.ExclusiveEndDate);
            }
        }
    }

    [Theory]
    [InlineData(19, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void ContinuationDatesIncludeEventUntilExclusiveEnd(int day, bool shouldInclude)
    {
        CalendarEventItem item = new CalendarEventItem();
        item.Start = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(9));
        item.End = item.Start.Value.AddDays(2);
        item.IsAllDay = true;
        Calendar page = new Calendar();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Calendar).GetField("mMonthEvents", flags)!.SetValue(page, new List<CalendarEventItem> { item });
        typeof(Calendar).GetField("mSelectedDayOrNull", flags)!.SetValue(page, new DateTime(2026, 10, day));
        IReadOnlyList<CalendarEventItem> selected = (IReadOnlyList<CalendarEventItem>)typeof(Calendar).GetMethod("getSelectedDayEvents", flags)!.Invoke(page, null)!;
        Assert.Equal(shouldInclude ? 1 : 0, selected.Count);
    }
}
