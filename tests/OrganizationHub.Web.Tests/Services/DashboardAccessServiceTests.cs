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

public sealed class DashboardAccessServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DashboardActionsRespectBothPermissionsAndDisabledMenusAsync(bool isCalendarDisabled)
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.CALENDAR_READ))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                PositionPermissionGrant request = new PositionPermissionGrant();
                request.PositionId = 1;
                request.PermissionKey = PermissionKeys.APPROVAL_REQUEST;
                request.IsAllowed = true;
                request.ScopeType = EPermissionScopeType.OwnTeam;
                db.PositionPermissionGrants.Add(request);
                NavigationMenuSection section = new NavigationMenuSection();
                section.DisplayName = "업무";
                section.SectionKey = "work";
                addMenu(db, section, NavigationMenuKey.CALENDAR, "/calendar", PermissionKeys.CALENDAR_READ, isCalendarDisabled == false);
                addMenu(db, section, NavigationMenuKey.TODOS, "/todos", PermissionKeys.TODOS_READ, true);
                addMenu(db, section, NavigationMenuKey.APPROVAL_HISTORY, "/approvals/history", PermissionKeys.APPROVAL_REQUEST, true);
                addMenu(db, section, NavigationMenuKey.APPROVAL_INBOX, "/approvals/inbox", PermissionKeys.APPROVAL_APPROVE, true);
                addMenu(db, section, NavigationMenuKey.MATERIALS, "/materials", PermissionKeys.FILES_READ, true);
                await db.SaveChangesAsync();
            }

            DashboardAccessService service = new DashboardAccessService(new NavigationMenuService(factory, new OrganizationPermissionService(factory)));
            DashboardAccess access = await service.GetAccessAsync(OperationPermissionRegressionTests.createSession(), CancellationToken.None);
            Assert.Equal(isCalendarDisabled == false, access.CanOpenCalendar);
            Assert.True(access.CanOpenHistory);
            Assert.False(access.CanOpenInbox);
            Assert.False(access.CanOpenTodos);
            Assert.False(access.CanOpenMaterials);
        }
    }

    private static void addMenu(OrganizationHubDbContext db, NavigationMenuSection section, string key, string path, string permission, bool isActive)
    {
        NavigationMenuItem menu = new NavigationMenuItem();
        menu.MenuKey = key;
        menu.DisplayName = key;
        menu.Href = path;
        menu.RequiredPermissionKeys = permission;
        menu.IsActive = isActive;
        menu.IsVisibleToAllTeams = true;
        menu.AccessLevel = EMenuAccessLevel.Authenticated;
        menu.Section = section;
        db.NavigationMenuItems.Add(menu);
    }
}
