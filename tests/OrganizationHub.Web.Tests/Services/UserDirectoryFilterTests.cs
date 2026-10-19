using System.Collections.Generic;
using System.Reflection;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Data.Entities;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class UserDirectoryFilterTests
{
    [Theory]
    [InlineData("ALICE", null, EUserActivationFilter.All, 1)]
    [InlineData("", 2, EUserActivationFilter.Active, 1)]
    [InlineData("", 2, EUserActivationFilter.Inactive, 1)]
    [InlineData(null, null, EUserActivationFilter.All, 2)]
    [InlineData("없는 사용자", null, EUserActivationFilter.All, 0)]
    public void UserSearchCombinesKeywordTeamAndActivation(string? keywordOrNull, int? teamIdOrNull, EUserActivationFilter status, int expectedCount)
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
        setField(page, "mUserSearchOrNull", keywordOrNull);
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
