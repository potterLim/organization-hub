using System.Linq;
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
