using System;
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

public sealed class NavigationMenuServiceTests
{
    [Fact]
    public async Task ManagementFilesMenuIsOnlyVisibleToManagementRolesAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.NavigationMenuItems.Add(createMenuItem("Materials", "팀 자료", "/materials", EMenuAccessLevel.TeamMember, 20));
                dbContext.NavigationMenuItems.Add(createMenuItem("ManagementFiles", "관리팀 자료", "/management-files", EMenuAccessLevel.Management, 30));
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory);
            UserSession teamUserSession = new UserSession(UserAccountId.CreateExisting(1), "planning.member", "기획팀원", EUserRole.TeamUser, OrganizationTeamId.CreateExisting(1), new OrganizationTeamKey("Planning"), "기획팀");
            UserSession managementSession = new UserSession(UserAccountId.CreateExisting(2), "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, null, OrganizationTeamKey.Empty, string.Empty);

            IReadOnlyList<NavigationMenuItem> teamMenus = await navigationMenuService.GetVisibleMenuItemsAsync(teamUserSession, CancellationToken.None);
            IReadOnlyList<NavigationMenuItem> managementMenus = await navigationMenuService.GetVisibleMenuItemsAsync(managementSession, CancellationToken.None);
            bool teamCanAccessManagementFiles = await navigationMenuService.CanAccessPathAsync(teamUserSession, NavigationPath.CreateOptional("/management-files"), CancellationToken.None);
            bool managementCanAccessManagementFiles = await navigationMenuService.CanAccessPathAsync(managementSession, NavigationPath.CreateOptional("/management-files"), CancellationToken.None);

            Assert.DoesNotContain(teamMenus, menuItem => menuItem.MenuKey == "ManagementFiles");
            Assert.Contains(managementMenus, menuItem => menuItem.MenuKey == "ManagementFiles");
            Assert.False(teamCanAccessManagementFiles);
            Assert.True(managementCanAccessManagementFiles);
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
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory);
            UserSession generalManagementSession = new UserSession(UserAccountId.CreateExisting(2), "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, null, OrganizationTeamKey.Empty, string.Empty);
            UserSession directorSession = new UserSession(UserAccountId.CreateExisting(3), "director", "디렉터", EUserRole.ManagementDirector, null, OrganizationTeamKey.Empty, string.Empty);
            UserSession managerSession = new UserSession(UserAccountId.CreateExisting(4), "manager", "매니저", EUserRole.ManagementManager, null, OrganizationTeamKey.Empty, string.Empty);
            UserSession adminSession = new UserSession(UserAccountId.CreateExisting(5), "admin", "관리자", EUserRole.Admin, null, OrganizationTeamKey.Empty, string.Empty);

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
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory);
            UserSession deputyLeaderSession = new UserSession(UserAccountId.CreateExisting(6), "planning.deputy", "기획 부책임자", EUserRole.TeamDeputyLeader, OrganizationTeamId.CreateExisting(1), new OrganizationTeamKey("Planning"), "기획팀");

            IReadOnlyList<NavigationMenuItem> menus = await navigationMenuService.GetVisibleMenuItemsAsync(deputyLeaderSession, CancellationToken.None);

            Assert.Contains(menus, menuItem => menuItem.MenuKey == "Materials");
            Assert.DoesNotContain(menus, menuItem => menuItem.MenuKey == "ResponsibleResources");
            Assert.False(await navigationMenuService.CanAccessPathAsync(deputyLeaderSession, NavigationPath.CreateOptional("/responsible-resources"), CancellationToken.None));
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
                dbContext.SaveChanges();
            }

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory);
            UserSession adminSession = new UserSession(UserAccountId.CreateExisting(5), "admin", "관리자", EUserRole.Admin, null, OrganizationTeamKey.Empty, string.Empty);

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

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory);

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

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory);
            UserSession userSession = new UserSession(UserAccountId.CreateExisting(21), "configured", "설정형 사용자", EUserRole.TeamUser, OrganizationTeamId.CreateExisting(1), new OrganizationTeamKey("Planning"), "기획팀");

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

            NavigationMenuService navigationMenuService = new NavigationMenuService(dbContextFactory);
            UserSession userSession = new UserSession(UserAccountId.CreateExisting(31), "team.context", "팀 맥락 사용자", EUserRole.TeamUser, OrganizationTeamId.CreateExisting(2), new OrganizationTeamKey("Support"), "고객지원팀");

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

    private static async Task seedConfiguredPermissionUserAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team team = new Team();
            team.Id = 1;
            team.TeamKey = "Planning";
            team.DisplayName = "기획팀";
            team.IsActive = true;
            OrganizationPosition position = new OrganizationPosition();
            position.Team = team;
            position.Name = "사용자 관리자";
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            addPermissionGrant(position, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
            addPermissionGrant(position, PermissionKeys.USERS_MANAGE, EPermissionScopeType.Global);
            UserAccount userAccount = new UserAccount();
            userAccount.Id = 21;
            userAccount.Username = "configured";
            userAccount.DisplayName = "설정형 사용자";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.TeamUser;
            userAccount.Team = team;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.Teams.Add(team);
            dbContext.Positions.Add(position);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static async Task seedConfiguredTeamContextUserAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team team = new Team();
            team.Id = 2;
            team.TeamKey = "Support";
            team.DisplayName = "고객지원팀";
            team.IsActive = true;
            OrganizationPosition position = new OrganizationPosition();
            position.Team = team;
            position.Name = "일정 담당자";
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            addPermissionGrant(position, PermissionKeys.CALENDAR_READ, EPermissionScopeType.OwnTeam);
            addPermissionGrant(position, PermissionKeys.TODOS_READ, EPermissionScopeType.OwnTeam);
            UserAccount userAccount = new UserAccount();
            userAccount.Id = 31;
            userAccount.Username = "team.context";
            userAccount.DisplayName = "팀 맥락 사용자";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.TeamUser;
            userAccount.Team = team;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.Teams.Add(team);
            dbContext.Positions.Add(position);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static void addPermissionGrant(OrganizationPosition position, string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
        permissionGrant.Position = position;
        permissionGrant.PermissionKey = permissionKey;
        permissionGrant.ScopeType = scopeType;
        permissionGrant.IsAllowed = true;
        position.PermissionGrants.Add(permissionGrant);
    }

    private static NavigationMenuItem createMenuItem(string menuKey, string displayName, string href, EMenuAccessLevel accessLevel, int sortOrder)
    {
        NavigationMenuItem menuItem = new NavigationMenuItem();
        menuItem.MenuKey = menuKey;
        menuItem.DisplayName = displayName;
        menuItem.Href = href;
        menuItem.IconKey = "Folder";
        menuItem.Section = createMenuSection(menuKey);
        menuItem.AccessLevel = accessLevel;
        menuItem.SortOrder = sortOrder;
        menuItem.IsActive = true;
        menuItem.IsVisibleToAllTeams = true;
        menuItem.IsLocked = false;
        return menuItem;
    }

    private static NavigationMenuSection createMenuSection(string menuKey)
    {
        NavigationMenuSection section = new NavigationMenuSection();
        section.SectionKey = "Test-" + menuKey;
        section.DisplayName = "테스트";
        section.SortOrder = 10;
        return section;
    }
}
