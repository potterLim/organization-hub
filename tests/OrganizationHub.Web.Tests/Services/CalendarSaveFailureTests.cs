using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CalendarSaveFailureTests
{
    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

    public class RejectedCalendarProxy : DispatchProxy
    {
        public bool IsCreationFailure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethodOrNull, object?[]? argsOrNull)
        {
            if (IsCreationFailure)
            {
                return Task.FromException<CalendarEventItem>(new ResourceCreationException("item", false, new Exception("metadata rejected")));
            }
            Google.GoogleApiException failure = new Google.GoogleApiException("calendar", "Rejected edit");
            failure.HttpStatusCode = HttpStatusCode.BadRequest;
            return Task.FromException<CalendarEventItem>(failure);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedCalendarSaveKeepsDraftAndExistingEventsAsync(bool isCreation)
    {
        Calendar page = new Calendar();
        typeof(OrganizationHubComponentBase).GetProperty("CurrentUserSessionOrNull", FLAGS)!.SetValue(page, OperationPermissionRegressionTests.createSession());
        ICalendarService service = DispatchProxy.Create<ICalendarService, RejectedCalendarProxy>();
        ((RejectedCalendarProxy)service).IsCreationFailure = isCreation;
        typeof(Calendar).GetProperty("CalendarService", FLAGS)!.SetValue(page, service);
        typeof(Calendar).GetProperty("Logger", FLAGS)!.SetValue(page, NullLogger<Calendar>.Instance);
        set(page, "mIsEditorOpen", true);
        set(page, "mIsEditing", isCreation == false);
        set(page, "mCurrentMonth", new DateTime(2026, 10, 1));
        set(page, "mEditorEventId", "item");
        set(page, "mEditorETag", "\"v1\"");
        set(page, "mEditorCalendarId", "calendar");
        set(page, "mEditorSummary", "아직 저장하지 못한 제목");
        set(page, "mEditorStartLocalOrNull", new DateTime(2026, 10, 19, 9, 0, 0));
        set(page, "mEditorEndLocalOrNull", new DateTime(2026, 10, 19, 10, 0, 0));
        List<CalendarReference> calendars = new List<CalendarReference>();
        calendars.Add(new CalendarReference(CalendarId.CreateRequired("calendar"), "공용", ECalendarViewKind.Organization, null));
        set(page, "mWritableCalendars", calendars);
        set(page, "mCreatableCalendars", calendars);
        CalendarEventItem original = new CalendarEventItem();
        original.Id = "item";
        original.Summary = "기존 일정";
        set(page, "mMonthEvents", new List<CalendarEventItem> { original });
        await (Task)typeof(Calendar).GetMethod("saveEventAsync", FLAGS)!.Invoke(page, null)!;
        Assert.True((bool)typeof(Calendar).GetField("mIsEditorOpen", FLAGS)!.GetValue(page)!);
        Assert.Equal("아직 저장하지 못한 제목", typeof(Calendar).GetField("mEditorSummary", FLAGS)!.GetValue(page));
        Assert.False((bool)typeof(Calendar).GetField("mIsSavingEvent", FLAGS)!.GetValue(page)!);
        Assert.Empty((string)typeof(Calendar).GetField("mErrorMessage", FLAGS)!.GetValue(page)!);
        Assert.Single((List<CalendarEventItem>)typeof(Calendar).GetField("mMonthEvents", FLAGS)!.GetValue(page)!);
    }

    private static void set(Calendar page, string name, object fieldValue)
    {
        typeof(Calendar).GetField(name, FLAGS)!.SetValue(page, fieldValue);
    }
}
