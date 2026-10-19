using System.Collections.Generic;
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

public sealed class AdministrativeOrderingConcurrencyTests
{
    [Theory]
    [InlineData("teams")]
    [InlineData("positions")]
    [InlineData("sections")]
    [InlineData("menus")]
    public async Task StaleOrderCannotReplaceSavedOrderAndFreshRetryWorksAsync(string resource)
    {
        using (TestDbContextFactory factory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                for (int id = 1; id <= 2; id++)
                {
                    Team team = new Team();
                    team.Id = id;
                    team.TeamKey = "team" + id;
                    team.DisplayName = team.TeamKey;
                    team.SortOrder = id * 10;
                    db.Teams.Add(team);
                    OrganizationPosition position = new OrganizationPosition();
                    position.Id = id;
                    position.TeamId = 1;
                    position.Name = "position" + id;
                    position.SortOrder = id * 10;
                    db.Positions.Add(position);
                    NavigationMenuSection section = new NavigationMenuSection();
                    section.Id = id;
                    section.SectionKey = "section" + id;
                    section.DisplayName = section.SectionKey;
                    section.SortOrder = id * 10;
                    db.NavigationMenuSections.Add(section);
                    NavigationMenuItem menu = new NavigationMenuItem();
                    menu.Id = id;
                    menu.SectionId = 1;
                    menu.MenuKey = "menu" + id;
                    menu.DisplayName = menu.MenuKey;
                    menu.Href = "/menu" + id;
                    menu.SortOrder = id * 10;
                    db.NavigationMenuItems.Add(menu);
                }
                await db.SaveChangesAsync();
            }
            Dictionary<int, int> stale = new Dictionary<int, int> { { 1, 0 }, { 2, 0 } };
            Assert.True((await saveOrderAsync(factory, resource, new int[] { 2, 1 }, stale)).IsSuccess);
            Assert.True((await saveOrderAsync(factory, resource, new int[] { 1, 2 }, stale)).IsConcurrencyConflict);
            Dictionary<int, int> fresh = new Dictionary<int, int> { { 1, 1 }, { 2, 1 } };
            Assert.True((await saveOrderAsync(factory, resource, new int[] { 1, 2 }, fresh)).IsSuccess);
        }
    }

    private static Task<ServiceResult> saveOrderAsync(TestDbContextFactory factory, string resource, IReadOnlyList<int> ids, IReadOnlyDictionary<int, int> revisions)
    {
        switch (resource)
        {
            case "teams":
                return new TeamManagementService(factory).UpdateTeamOrderAsync(new OrganizationTeamOrderUpdateRequest(ids.Select(OrganizationTeamId.CreateExisting).ToList(), revisions), CancellationToken.None);
            case "positions":
                return new PositionManagementService(factory).UpdatePositionOrderAsync(new OrganizationPositionOrderUpdateRequest(OrganizationTeamId.CreateExisting(1), ids.Select(OrganizationPositionId.CreateExisting).ToList(), revisions), CancellationToken.None);
            case "sections":
                return new NavigationMenuService(factory, new OrganizationPermissionService(factory)).UpdateMenuSectionOrderAsync(new NavigationMenuSectionOrderUpdateRequest(ids.Select(NavigationMenuSectionId.CreateExisting).ToList(), revisions), CancellationToken.None);
            case "menus":
                return new NavigationMenuService(factory, new OrganizationPermissionService(factory)).UpdateMenuItemOrderAsync(new NavigationMenuItemOrderUpdateRequest(ids.Select(NavigationMenuItemId.CreateExisting).ToList(), revisions), CancellationToken.None);
            default:
                throw new System.ArgumentOutOfRangeException(nameof(resource));
        }
    }
}
