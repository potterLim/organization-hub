using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    [Fact]
    public async Task TeamOnlyAccessChangeInvalidatesStaleMenuFormAsync()
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                db.Teams.Add(createTeam(1, "first", "첫 팀", 10));
                db.Teams.Add(createTeam(2, "second", "다음 팀", 20));
                NavigationMenuItem menu = createMenuItem(1, "Custom", "자료", "/custom", EMenuAccessLevel.TeamMember, 10);
                menu.IsVisibleToAllTeams = false;
                NavigationMenuTeamPermission permission = new NavigationMenuTeamPermission();
                permission.NavigationMenuItem = menu;
                permission.TeamId = 1;
                menu.TeamPermissions.Add(permission);
                db.NavigationMenuItems.Add(menu);
                await db.SaveChangesAsync();
            }
            NavigationMenuService service = new NavigationMenuService(factory, new OrganizationPermissionService(factory));
            NavigationMenuItemFormModel form = new NavigationMenuItemFormModel();
            form.Id = 1;
            form.SectionId = 1;
            form.DisplayName = "자료";
            form.SortOrder = 10;
            form.AccessLevel = EMenuAccessLevel.TeamMember;
            form.IsActive = true;
            form.TeamIds.Add(2);
            Assert.True((await service.UpdateMenuItemAsync(form, CancellationToken.None)).IsSuccess);
            form.TeamIds.Clear();
            form.TeamIds.Add(1);
            Assert.True((await service.UpdateMenuItemAsync(form, CancellationToken.None)).IsConcurrencyConflict);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Equal(2, db.NavigationMenuTeamPermissions.Single().TeamId);
                Assert.Equal(1, db.NavigationMenuItems.Single().Revision);
            }
        }
    }

    [Fact]
    public async Task StaleMenuRenameDoesNotRestoreRevokedAccessAsync()
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                db.NavigationMenuItems.Add(createMenuItem(1, "Calendar", "일정", "/calendar", EMenuAccessLevel.Authenticated, 10));
                await db.SaveChangesAsync();
            }
            NavigationMenuService service = new NavigationMenuService(factory, new OrganizationPermissionService(factory));
            NavigationMenuItemFormModel first = new NavigationMenuItemFormModel();
            first.Id = 1;
            first.SectionId = 1;
            first.DisplayName = "일정";
            first.AccessLevel = EMenuAccessLevel.Authenticated;
            first.IsActive = false;
            Assert.True((await service.UpdateMenuItemAsync(first, CancellationToken.None)).IsSuccess);
            NavigationMenuItemFormModel stale = new NavigationMenuItemFormModel();
            stale.Id = 1;
            stale.SectionId = 1;
            stale.DisplayName = "일정 이름 변경";
            stale.AccessLevel = EMenuAccessLevel.Authenticated;
            stale.IsActive = true;
            ServiceResult result = await service.UpdateMenuItemAsync(stale, CancellationToken.None);
            Assert.True(result.IsConcurrencyConflict);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.False(db.NavigationMenuItems.Single().IsActive);
            }
        }
    }
}
