using System.Reflection;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DraftProtectionTests
{
    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void SavedDraftClearsPendingNavigationWarning()
    {
        UnsavedChangesGuard guard = new UnsavedChangesGuard();
        typeof(UnsavedChangesGuard).GetField("mPendingLocationOrNull", FLAGS)!.SetValue(guard, "/todos");
        typeof(UnsavedChangesGuard).GetMethod("OnParametersSet", FLAGS)!.Invoke(guard, null);
        Assert.Null(typeof(UnsavedChangesGuard).GetField("mPendingLocationOrNull", FLAGS)!.GetValue(guard));
    }

    [Fact]
    public void UserFormDetectsChangedNameAndPasswordWithoutReportingUnchangedForm()
    {
        AdminUsers page = new AdminUsers();
        typeof(AdminUsers).GetMethod("clearForm", FLAGS)!.Invoke(page, null);
        Assert.False(hasChanges(page));
        UserAccountFormModel form = (UserAccountFormModel)typeof(AdminUsers).GetField("mFormModel", FLAGS)!.GetValue(page)!;
        form.DisplayName = "새 이름";
        Assert.True(hasChanges(page));
        form.DisplayName = string.Empty;
        Assert.False(hasChanges(page));
        typeof(AdminUsers).GetField("mResetPassword", FLAGS)!.SetValue(page, "unsaved-password");
        Assert.True(hasChanges(page));
    }

    private static bool hasChanges(AdminUsers page)
    {
        return (bool)typeof(AdminUsers).GetMethod("hasFormChanges", FLAGS)!.Invoke(page, null)!;
    }
}
