using System.Collections.Generic;
using System.Reflection;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Data.Entities;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class UserDirectoryFilterTests
{
    [Theory]
    [InlineData("ALICE", null, "all", 1)]
    [InlineData("", 2, "active", 1)]
    [InlineData("", 2, "inactive", 1)]
    [InlineData("없는 사용자", null, "all", 0)]
    public void UserSearchCombinesKeywordTeamAndActivation(string keyword, int? teamIdOrNull, string status, int expectedCount)
    {
        AdminUsers page = new AdminUsers();
        UserAccount first = new UserAccount();
        first.Username = "alice";
        first.DisplayName = "김서연";
        first.TeamId = 2;
        first.IsActive = true;
        UserAccount second = new UserAccount();
        second.Username = "bob";
        second.DisplayName = "박지우";
        second.TeamId = 2;
        second.IsActive = false;
        setField(page, "mUsers", new List<UserAccount> { first, second });
        setField(page, "mUserSearch", keyword);
        setField(page, "mFilterTeamIdOrNull", teamIdOrNull);
        setField(page, "mFilterStatus", status);
        IReadOnlyList<UserAccount> users = (IReadOnlyList<UserAccount>)typeof(AdminUsers).GetMethod("getFilteredUsers", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        Assert.Equal(expectedCount, users.Count);
    }

    private static void setField(AdminUsers page, string name, object? fieldValueOrNull)
    {
        typeof(AdminUsers).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, fieldValueOrNull);
    }
}
