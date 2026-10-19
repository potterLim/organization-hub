using System;
using System.Reflection;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CalendarDraftRegressionTests
{
    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrowsingAnotherDayKeepsTheActiveDraftAndEditTarget(bool isEditing)
    {
        Calendar page = createDraft(isEditing);
        CalendarDayCell day = new CalendarDayCell();
        day.Date = new DateTime(2026, 10, 21);
        invoke(page, "selectDay", day);
        assertDraft(page);
        Assert.Equal("original", get<CalendarEventItem>(page, "mSelectedEventOrNull").Id);
        invoke(page, "clearSelectedDay");
        assertDraft(page);
    }

    [Fact]
    public void OpeningAnotherItemOrCreateAgainCannotOverwriteTheActiveDraft()
    {
        Calendar page = createDraft(true);
        CalendarEventItem other = new CalendarEventItem();
        other.Id = "other";
        other.CanEdit = true;
        other.Summary = "다른 일정";
        invoke(page, "selectEvent", other);
        invoke(page, "openEditEditor", other);
        invoke(page, "openCreateEditor");
        assertDraft(page);
        Assert.Equal("original", get<CalendarEventItem>(page, "mSelectedEventOrNull").Id);
    }

    [Fact]
    public void CancelRequiresExplicitDiscardAndKeepWritingPreservesChanges()
    {
        Calendar page = createDraft(false);
        invoke(page, "requestCloseEditor");
        assertDraft(page);
        Assert.True(get<bool>(page, "mIsDiscardConfirmationOpen"));
        invoke(page, "keepEditorDraft");
        assertDraft(page);
        Assert.False(get<bool>(page, "mIsDiscardConfirmationOpen"));
        invoke(page, "requestCloseEditor");
        invoke(page, "closeEditor");
        Assert.False(get<bool>(page, "mIsEditorOpen"));
        Assert.False(get<bool>(page, "mIsDiscardConfirmationOpen"));
    }

    private static Calendar createDraft(bool isEditing)
    {
        Calendar page = new Calendar();
        CalendarEventItem original = new CalendarEventItem();
        original.Id = "original";
        set(page, "mIsEditorOpen", true);
        set(page, "mIsEditing", isEditing);
        set(page, "mSelectedEventOrNull", original);
        set(page, "mEditorSummary", "저장 전 제목");
        set(page, "mEditorDescription", "저장 전 상세 설명");
        set(page, "mEditorLocation", "회의실 A");
        return page;
    }

    private static void assertDraft(Calendar page)
    {
        Assert.True(get<bool>(page, "mIsEditorOpen"));
        Assert.Equal("저장 전 제목", get<string>(page, "mEditorSummary"));
        Assert.Equal("저장 전 상세 설명", get<string>(page, "mEditorDescription"));
        Assert.Equal("회의실 A", get<string>(page, "mEditorLocation"));
    }

    private static void invoke(Calendar page, string method, params object[] arguments)
    {
        typeof(Calendar).GetMethod(method, FLAGS)!.Invoke(page, arguments);
    }

    private static void set(Calendar page, string field, object value)
    {
        typeof(Calendar).GetField(field, FLAGS)!.SetValue(page, value);
    }

    private static T get<T>(Calendar page, string field)
    {
        return (T)typeof(Calendar).GetField(field, FLAGS)!.GetValue(page)!;
    }
}
