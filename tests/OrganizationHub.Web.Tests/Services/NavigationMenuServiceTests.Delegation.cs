using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class NavigationMenuServiceTests
{
    [Theory]
    [InlineData("Calendar", "/calendar", PermissionKeys.CALENDAR_READ)]
    [InlineData("Todos", "/todos", PermissionKeys.TODOS_READ)]
    [InlineData("Materials", "/materials", PermissionKeys.FILES_READ)]
    public async Task DelegatedTeamReadAllowsEntryWithoutGrantingOwnTeamAccessAsync(string key, string path, string permission)
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        {
            await seedConfiguredPermissionUserAsync(factory);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Team target = createTeam(2, "Support", "지원팀", 20);
                db.Teams.Add(target);
                OrganizationPosition position = await db.Positions.Include(item => item.PermissionGrants).SingleAsync();
                position.PermissionGrants.Clear();
                PositionPermissionGrant grant = new PositionPermissionGrant();
                grant.PermissionKey = permission;
                grant.ScopeType = EPermissionScopeType.SelectedTeam;
                grant.TargetTeam = target;
                grant.IsAllowed = true;
                position.PermissionGrants.Add(grant);
                NavigationMenuItem menu = createMenuItem(key, key, path, EMenuAccessLevel.TeamMember, 20);
                menu.RequiredPermissionKeys = permission;
                menu.IsVisibleToAllTeams = true;
                db.NavigationMenuItems.Add(menu);
                await db.SaveChangesAsync();
            }
            UserSession session = new UserSession(new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(21),
                Username = Username.CreateOptional("configured"),
                DisplayName = DisplayName.CreateOptional("설정형 사용자"),
                TeamDisplayName = DisplayName.CreateOptional("기획팀"),
                TeamKey = new OrganizationTeamKey("Planning"),
                StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser,
                OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1)
            });
            OrganizationPermissionService permissions = new OrganizationPermissionService(factory);
            NavigationMenuService menus = new NavigationMenuService(factory, permissions);
            Assert.True(await menus.CanAccessPathAsync(session, NavigationPath.CreateOptional(path), CancellationToken.None));
            Assert.Contains(await menus.GetVisibleMenuItemsAsync(session, CancellationToken.None), item => item.MenuKey == key);
            Assert.False(await permissions.HasPermissionAsync(session, new PermissionKey(permission), OrganizationTeamId.CreateExisting(1), CancellationToken.None));
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Team target = await db.Teams.SingleAsync(item => item.Id == 2);
                target.IsActive = false;
                await db.SaveChangesAsync();
            }
            Assert.False(await menus.CanAccessPathAsync(session, NavigationPath.CreateOptional(path), CancellationToken.None));
        }
    }
}
