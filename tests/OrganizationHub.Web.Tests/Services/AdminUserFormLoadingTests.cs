using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OrganizationHub.Web.Components;
using OrganizationHub.Web.Components.Pages;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class AdminUserFormLoadingTests
{
    private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

    public class DirectoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? methodOrNull, object?[]? argsOrNull)
        {
            Team team = new Team { Id = 2, IsActive = true, TeamKey = "team", DisplayName = "팀" };
            if (methodOrNull!.Name == "GetTeamsAsync")
            {
                return Task.FromResult<IReadOnlyList<Team>>(new List<Team> { team });
            }
            if (methodOrNull.Name == "GetPositionsAsync")
            {
                return Task.FromResult<IReadOnlyList<OrganizationPosition>>(new List<OrganizationPosition>
                {
                    new OrganizationPosition { Id = 3, TeamId = 2, Team = team, IsActive = true, Name = "담당자" }
                });
            }
            return Task.FromResult<IReadOnlyList<UserAccount>>(new List<UserAccount>());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadingDefaultsDoesNotInventChangesOrEraseRealInputAsync(bool hasTypedInput)
    {
        AdminUsers page = new AdminUsers();
        typeof(OrganizationHubComponentBase).GetProperty("CurrentUserSessionOrNull", FLAGS)!.SetValue(page, OperationPermissionRegressionTests.createSession());
        typeof(AdminUsers).GetField("mCanManageSystemAdmins", FLAGS)!.SetValue(page, true);
        typeof(AdminUsers).GetProperty("UserManagementService", FLAGS)!.SetValue(page, DispatchProxy.Create<IUserManagementService, DirectoryProxy>());
        typeof(AdminUsers).GetProperty("TeamManagementService", FLAGS)!.SetValue(page, DispatchProxy.Create<ITeamManagementService, DirectoryProxy>());
        typeof(AdminUsers).GetProperty("PositionManagementService", FLAGS)!.SetValue(page, DispatchProxy.Create<IPositionManagementService, DirectoryProxy>());
        typeof(AdminUsers).GetMethod("clearForm", FLAGS)!.Invoke(page, null);
        UserAccountFormModel form = (UserAccountFormModel)typeof(AdminUsers).GetField("mFormModel", FLAGS)!.GetValue(page)!;
        form.DisplayName = hasTypedInput ? "작성 중인 이름" : string.Empty;
        await (Task)typeof(AdminUsers).GetMethod("loadDataAsync", FLAGS)!.Invoke(page, null)!;
        Assert.Equal(2, form.TeamId);
        Assert.Equal(3, form.PositionId);
        Assert.Equal(hasTypedInput ? "작성 중인 이름" : string.Empty, form.DisplayName);
        Assert.Equal(hasTypedInput, (bool)typeof(AdminUsers).GetMethod("hasFormChanges", FLAGS)!.Invoke(page, null)!);
    }
}
