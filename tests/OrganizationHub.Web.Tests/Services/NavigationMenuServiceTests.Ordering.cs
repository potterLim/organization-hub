using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class NavigationMenuServiceTests
{
    [Fact]
    public async Task UpdateMenuSectionOrderRejectsDuplicateSectionIdsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuSections.Add(createMenuSection(1, "Operations", "운영", 10));
                dbContext.NavigationMenuSections.Add(createMenuSection(2, "Resources", "자료실", 20));
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            List<NavigationMenuSectionId> orderedSectionIds = new List<NavigationMenuSectionId>
            {
                NavigationMenuSectionId.CreateExisting(1),
                NavigationMenuSectionId.CreateExisting(1)
            };

            ServiceResult result = await navigationMenuService.UpdateMenuSectionOrderAsync(new NavigationMenuSectionOrderUpdateRequest(orderedSectionIds), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("정렬할 메뉴 섹션이 중복되어 있습니다.", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task UpdateMenuItemOrderRejectsDuplicateMenuItemIdsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem(1, "Dashboard", "홈", "/dashboard", EMenuAccessLevel.Authenticated, 10));
                dbContext.NavigationMenuItems.Add(createMenuItem(2, "Calendar", "일정", "/calendar", EMenuAccessLevel.Authenticated, 20));
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            List<NavigationMenuItemId> orderedMenuItemIds = new List<NavigationMenuItemId>
            {
                NavigationMenuItemId.CreateExisting(1),
                NavigationMenuItemId.CreateExisting(1)
            };

            ServiceResult result = await navigationMenuService.UpdateMenuItemOrderAsync(new NavigationMenuItemOrderUpdateRequest(orderedMenuItemIds), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("정렬할 메뉴가 중복되어 있습니다.", result.ErrorMessage);
        }
    }
}
