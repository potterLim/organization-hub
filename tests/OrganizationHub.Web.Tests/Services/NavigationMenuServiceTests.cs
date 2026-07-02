using System.Collections.Generic;
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
    public async Task ManagementFilesMenuIsOnlyVisibleToManagementTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("Materials", "팀 자료", "/materials", EMenuAccessLevel.TeamMember, 20));
                dbContext.NavigationMenuItems.Add(createMenuItem("ManagementFiles", "관리팀 자료", "/management-files", EMenuAccessLevel.Management, 30));
                seedMenuUsers(dbContext);
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession teamUserSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(1),
                    Username = Username.CreateOptional("planning.member"),
                    DisplayName = DisplayName.CreateOptional("기획팀원"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });
            UserSession managementSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(2),
                    Username = Username.CreateOptional("management.staff"),
                    DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });

            IReadOnlyList<NavigationMenuItem> teamMenus = await navigationMenuService.GetVisibleMenuItemsAsync(teamUserSession, CancellationToken.None);
            IReadOnlyList<NavigationMenuItem> managementMenus = await navigationMenuService.GetVisibleMenuItemsAsync(managementSession, CancellationToken.None);
            bool canTeamAccessManagementFiles = await navigationMenuService.CanAccessPathAsync(teamUserSession, NavigationPath.CreateOptional("/management-files"), CancellationToken.None);
            bool canManagementAccessManagementFiles = await navigationMenuService.CanAccessPathAsync(managementSession, NavigationPath.CreateOptional("/management-files"), CancellationToken.None);

            Assert.DoesNotContain(teamMenus, menuItem => menuItem.MenuKey == "ManagementFiles");
            Assert.Contains(managementMenus, menuItem => menuItem.MenuKey == "ManagementFiles");
            Assert.False(canTeamAccessManagementFiles);
            Assert.True(canManagementAccessManagementFiles);
        }
    }

    [Fact]
    public async Task ManagementMenusSeparateGeneralManagementManagerLevelAndAdminAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminUsers", "사용자 관리", "/admin/users", EMenuAccessLevel.ManagementManager, 300));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminCalendarAudit", "일정 삭제 기록", "/admin/calendar-audit", EMenuAccessLevel.ManagementManager, 350));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminTeams", "팀 관리", "/admin/teams", EMenuAccessLevel.Admin, 310));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminSettings", "시스템 설정", "/admin/settings", EMenuAccessLevel.Admin, 320));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminMenus", "메뉴 관리", "/admin/menus", EMenuAccessLevel.Admin, 330));
                seedMenuUsers(dbContext);
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession generalManagementSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(2),
                    Username = Username.CreateOptional("management.staff"),
                    DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });
            UserSession directorSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(3),
                    Username = Username.CreateOptional("director"),
                    DisplayName = DisplayName.CreateOptional("디렉터"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementDirector,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });
            UserSession managerSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(4),
                    Username = Username.CreateOptional("manager"),
                    DisplayName = DisplayName.CreateOptional("매니저"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementManager,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });
            UserSession adminSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(5),
                    Username = Username.CreateOptional("admin"),
                    DisplayName = DisplayName.CreateOptional("관리자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.Admin,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });

            IReadOnlyList<NavigationMenuItem> generalManagementMenus = await navigationMenuService.GetVisibleMenuItemsAsync(generalManagementSession, CancellationToken.None);
            IReadOnlyList<NavigationMenuItem> directorMenus = await navigationMenuService.GetVisibleMenuItemsAsync(directorSession, CancellationToken.None);
            IReadOnlyList<NavigationMenuItem> managerMenus = await navigationMenuService.GetVisibleMenuItemsAsync(managerSession, CancellationToken.None);
            IReadOnlyList<NavigationMenuItem> adminMenus = await navigationMenuService.GetVisibleMenuItemsAsync(adminSession, CancellationToken.None);

            Assert.DoesNotContain(generalManagementMenus, menuItem => menuItem.MenuKey == "AdminUsers");
            Assert.DoesNotContain(generalManagementMenus, menuItem => menuItem.MenuKey == "AdminTeams");
            Assert.Contains(directorMenus, menuItem => menuItem.MenuKey == "AdminUsers");
            Assert.Contains(directorMenus, menuItem => menuItem.MenuKey == "AdminCalendarAudit");
            Assert.DoesNotContain(directorMenus, menuItem => menuItem.MenuKey == "AdminTeams");
            Assert.Contains(managerMenus, menuItem => menuItem.MenuKey == "AdminUsers");
            Assert.Contains(managerMenus, menuItem => menuItem.MenuKey == "AdminCalendarAudit");
            Assert.DoesNotContain(managerMenus, menuItem => menuItem.MenuKey == "AdminMenus");
            Assert.Contains(adminMenus, menuItem => menuItem.MenuKey == "AdminUsers");
            Assert.Contains(adminMenus, menuItem => menuItem.MenuKey == "AdminTeams");
            Assert.Contains(adminMenus, menuItem => menuItem.MenuKey == "AdminSettings");
            Assert.Contains(adminMenus, menuItem => menuItem.MenuKey == "AdminMenus");
            Assert.False(await navigationMenuService.CanAccessPathAsync(generalManagementSession, NavigationPath.CreateOptional("/admin/users"), CancellationToken.None));
            Assert.True(await navigationMenuService.CanAccessPathAsync(managerSession, NavigationPath.CreateOptional("/admin/users"), CancellationToken.None));
            Assert.False(await navigationMenuService.CanAccessPathAsync(managerSession, NavigationPath.CreateOptional("/admin/menus"), CancellationToken.None));
            Assert.True(await navigationMenuService.CanAccessPathAsync(adminSession, NavigationPath.CreateOptional("/admin/menus"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task TeamDeputyLeaderDoesNotReceiveTeamLeaderMenusAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("ResponsibleResources", "책임자 자료", "/responsible-resources", EMenuAccessLevel.TeamLeader, 110));
                dbContext.NavigationMenuItems.Add(createMenuItem("Materials", "팀 자료", "/materials", EMenuAccessLevel.TeamMember, 20));
                seedMenuUsers(dbContext);
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession deputyLeaderSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(6),
                    Username = Username.CreateOptional("planning.deputy"),
                    DisplayName = DisplayName.CreateOptional("기획 부책임자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.TeamDeputyLeader,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });

            IReadOnlyList<NavigationMenuItem> menus = await navigationMenuService.GetVisibleMenuItemsAsync(deputyLeaderSession, CancellationToken.None);

            Assert.Contains(menus, menuItem => menuItem.MenuKey == "Materials");
            Assert.Contains(menus, menuItem => menuItem.MenuKey == "ResponsibleResources");
            Assert.True(await navigationMenuService.CanAccessPathAsync(deputyLeaderSession, NavigationPath.CreateOptional("/responsible-resources"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task AdministratorReceivesAllActiveMenusAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("Dashboard", "홈", "/dashboard", EMenuAccessLevel.Authenticated, 10));
                dbContext.NavigationMenuItems.Add(createMenuItem("ApprovalRequest", "결재 요청", "/approvals/request", EMenuAccessLevel.TeamMember, 20));
                dbContext.NavigationMenuItems.Add(createMenuItem("ManagementFiles", "관리팀 자료", "/management-files", EMenuAccessLevel.Management, 30));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminMenus", "메뉴 관리", "/admin/menus", EMenuAccessLevel.Admin, 40));
                seedMenuUsers(dbContext);
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession adminSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(5),
                    Username = Username.CreateOptional("admin"),
                    DisplayName = DisplayName.CreateOptional("관리자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.Admin,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                    TeamKey = new OrganizationTeamKey("Management"),
                    TeamDisplayName = DisplayName.CreateOptional("관리팀")
                });

            IReadOnlyList<NavigationMenuItem> menus = await navigationMenuService.GetVisibleMenuItemsAsync(adminSession, CancellationToken.None);

            Assert.Contains(menus, menuItem => menuItem.MenuKey == "Dashboard");
            Assert.Contains(menus, menuItem => menuItem.MenuKey == "ApprovalRequest");
            Assert.Contains(menus, menuItem => menuItem.MenuKey == "ManagementFiles");
            Assert.Contains(menus, menuItem => menuItem.MenuKey == "AdminMenus");
            Assert.True(await navigationMenuService.CanAccessPathAsync(adminSession, NavigationPath.CreateOptional("/approvals/request"), CancellationToken.None));
            Assert.True(await navigationMenuService.CanAccessPathAsync(adminSession, NavigationPath.CreateOptional("/management-files"), CancellationToken.None));
            Assert.True(await navigationMenuService.CanAccessPathAsync(adminSession, NavigationPath.CreateOptional("/admin/menus"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task ManageableMenuItemsExcludeRetiredDefaultMenusAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("Dashboard", "홈", "/dashboard", EMenuAccessLevel.Authenticated, 10));
                dbContext.NavigationMenuItems.Add(createMenuItem("Rules", "Retired menu A", "/rules", EMenuAccessLevel.Authenticated, 20));
                dbContext.NavigationMenuItems.Add(createMenuItem("OnSite", "Retired menu B", "/on-site", EMenuAccessLevel.Authenticated, 30));
                dbContext.NavigationMenuItems.Add(createMenuItem("Leaders", "Retired menu C", "/leaders", EMenuAccessLevel.TeamLeader, 40));
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));

            IReadOnlyList<NavigationMenuItem> menuItems = await navigationMenuService.GetManageableMenuItemsAsync(CancellationToken.None);

            Assert.Contains(menuItems, menuItem => menuItem.MenuKey == "Dashboard");
            Assert.DoesNotContain(menuItems, menuItem => menuItem.Href == "/rules");
            Assert.DoesNotContain(menuItems, menuItem => menuItem.Href == "/on-site");
            Assert.DoesNotContain(menuItems, menuItem => menuItem.Href == "/leaders");
        }
    }

    [Fact]
    public async Task ConfiguredPermissionUserReceivesOnlyGrantedMenusAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedConfiguredPermissionUserAsync(dbContextFactory);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("Materials", "팀 자료", "/materials", EMenuAccessLevel.TeamMember, 20));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminUsers", "사용자 관리", "/admin/users", EMenuAccessLevel.ManagementManager, 300));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminMenus", "메뉴 관리", "/admin/menus", EMenuAccessLevel.Admin, 330));
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession userSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(21),
                    Username = Username.CreateOptional("configured"),
                    DisplayName = DisplayName.CreateOptional("설정형 사용자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });

            IReadOnlyList<NavigationMenuItem> menus = await navigationMenuService.GetVisibleMenuItemsAsync(userSession, CancellationToken.None);

            Assert.Contains(menus, menuItem => menuItem.MenuKey == "Materials");
            Assert.Contains(menus, menuItem => menuItem.MenuKey == "AdminUsers");
            Assert.DoesNotContain(menus, menuItem => menuItem.MenuKey == "AdminMenus");
            Assert.True(await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/admin/users"), CancellationToken.None));
            Assert.False(await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/admin/menus"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task ConfiguredOwnTeamPermissionShowsTeamContextMenusAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedConfiguredTeamContextUserAsync(dbContextFactory);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("Calendar", "일정", "/calendar", EMenuAccessLevel.Authenticated, 20));
                dbContext.NavigationMenuItems.Add(createMenuItem("Todos", "할 일", "/todos", EMenuAccessLevel.Authenticated, 30));
                dbContext.NavigationMenuItems.Add(createMenuItem("ApprovalRequest", "결재 요청", "/approvals/request", EMenuAccessLevel.TeamMember, 40));
                dbContext.NavigationMenuItems.Add(createMenuItem("AdminSettings", "기본 설정", "/admin/settings", EMenuAccessLevel.Admin, 340));
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession userSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(31),
                    Username = Username.CreateOptional("team.context"),
                    DisplayName = DisplayName.CreateOptional("팀 맥락 사용자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                    TeamKey = new OrganizationTeamKey("Support"),
                    TeamDisplayName = DisplayName.CreateOptional("고객지원팀")
                });

            IReadOnlyList<NavigationMenuItem> menus = await navigationMenuService.GetVisibleMenuItemsAsync(userSession, CancellationToken.None);

            Assert.Contains(menus, menuItem => menuItem.MenuKey == "Calendar");
            Assert.Contains(menus, menuItem => menuItem.MenuKey == "Todos");
            Assert.DoesNotContain(menus, menuItem => menuItem.MenuKey == "ApprovalRequest");
            Assert.DoesNotContain(menus, menuItem => menuItem.MenuKey == "AdminSettings");
            Assert.True(await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/calendar"), CancellationToken.None));
            Assert.True(await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/todos"), CancellationToken.None));
            Assert.False(await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/approvals/request"), CancellationToken.None));
            Assert.False(await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/admin/settings"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task UnregisteredProtectedPathIsDeniedAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("Dashboard", "홈", "/dashboard", EMenuAccessLevel.Authenticated, 10));
                seedMenuUsers(dbContext);
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession userSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(1),
                    Username = Username.CreateOptional("planning.member"),
                    DisplayName = DisplayName.CreateOptional("기획팀원"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });

            bool canAccessRoot = await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/"), CancellationToken.None);
            bool canAccessUnknownPath = await navigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/admin/unregistered"), CancellationToken.None);

            Assert.True(canAccessRoot);
            Assert.False(canAccessUnknownPath);
        }
    }

}
