using System;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Tests.Infrastructure;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class NavigationMenuServiceTests
{
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
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser;
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
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser;
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

    private static void seedMenuUsers(OrganizationHubDbContext dbContext)
    {
        Team planningTeam = createTeam(1, "Planning", "기획팀", 10);
        Team managementTeam = createTeam(2, "Management", "관리팀", 20);
        OrganizationPosition planningPosition = createPosition(planningTeam, "기획팀 구성원", 10, false);
        addPermissionGrant(planningPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
        addPermissionGrant(planningPosition, PermissionKeys.APPROVAL_REQUEST, EPermissionScopeType.OwnTeam);

        OrganizationPosition managementStaffPosition = createPosition(managementTeam, "관리팀 담당자", 10, false);
        addPermissionGrant(managementStaffPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);

        OrganizationPosition directorPosition = createPosition(managementTeam, "관리 책임자", 20, false);
        addPermissionGrant(directorPosition, PermissionKeys.USERS_MANAGE, EPermissionScopeType.Global);
        addPermissionGrant(directorPosition, PermissionKeys.CALENDAR_AUDIT_READ, EPermissionScopeType.Global);

        OrganizationPosition managerPosition = createPosition(managementTeam, "관리 매니저", 30, false);
        addPermissionGrant(managerPosition, PermissionKeys.USERS_MANAGE, EPermissionScopeType.Global);
        addPermissionGrant(managerPosition, PermissionKeys.CALENDAR_AUDIT_READ, EPermissionScopeType.Global);

        OrganizationPosition systemAdministratorPosition = createPosition(managementTeam, "시스템 관리자", 40, true);
        OrganizationPosition deputyPosition = createPosition(planningTeam, "기획 부책임자", 20, false);
        addPermissionGrant(deputyPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);

        dbContext.Teams.Add(planningTeam);
        dbContext.Teams.Add(managementTeam);
        dbContext.Positions.Add(planningPosition);
        dbContext.Positions.Add(managementStaffPosition);
        dbContext.Positions.Add(directorPosition);
        dbContext.Positions.Add(managerPosition);
        dbContext.Positions.Add(systemAdministratorPosition);
        dbContext.Positions.Add(deputyPosition);
        dbContext.UserAccounts.Add(createUser(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, planningTeam, planningPosition));
        dbContext.UserAccounts.Add(createUser(2, "management.staff", "관리팀 담당자", EStoredCompatibilityRole.ManagementStaff, managementTeam, managementStaffPosition));
        dbContext.UserAccounts.Add(createUser(3, "director", "디렉터", EStoredCompatibilityRole.ManagementDirector, managementTeam, directorPosition));
        dbContext.UserAccounts.Add(createUser(4, "manager", "매니저", EStoredCompatibilityRole.ManagementManager, managementTeam, managerPosition));
        dbContext.UserAccounts.Add(createUser(5, "admin", "관리자", EStoredCompatibilityRole.Admin, managementTeam, systemAdministratorPosition));
        dbContext.UserAccounts.Add(createUser(6, "planning.deputy", "기획 부책임자", EStoredCompatibilityRole.TeamDeputyLeader, planningTeam, deputyPosition));
    }

    private static Team createTeam(int id, string teamKey, string displayName, int sortOrder)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private static OrganizationPosition createPosition(Team team, string name, int sortOrder, bool isSystemAdministrator)
    {
        OrganizationPosition position = new OrganizationPosition();
        position.Team = team;
        position.Name = name;
        position.Description = string.Empty;
        position.SortOrder = sortOrder;
        position.IsActive = true;
        position.IsSystemAdministrator = isSystemAdministrator;
        return position;
    }

    private static UserAccount createUser(
        int id,
        string username,
        string displayName,
        EStoredCompatibilityRole role,
        Team team,
        OrganizationPosition position)
    {
        UserAccount userAccount = new UserAccount();
        userAccount.Id = id;
        userAccount.Username = username;
        userAccount.DisplayName = displayName;
        userAccount.PasswordHash = "test";
        userAccount.StoredCompatibilityRole = role;
        userAccount.Team = team;
        userAccount.Position = position;
        userAccount.IsActive = true;
        userAccount.CreatedAt = DateTime.UtcNow;
        return userAccount;
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
        menuItem.RequiredPermissionKeys = NavigationMenuPermissionKeys.CreateStorageValue(
            NavigationMenuPermissionCatalog.GetRequiredPermissionKeys(NavigationMenuKey.CreateRequired(menuKey)));
        menuItem.SortOrder = sortOrder;
        menuItem.IsActive = true;
        menuItem.IsVisibleToAllTeams = true;
        menuItem.IsLocked = false;
        return menuItem;
    }

    private static NavigationMenuItem createMenuItem(int id, string menuKey, string displayName, string href, EMenuAccessLevel accessLevel, int sortOrder)
    {
        NavigationMenuItem menuItem = createMenuItem(menuKey, displayName, href, accessLevel, sortOrder);
        menuItem.Id = id;
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

    private static NavigationMenuSection createMenuSection(int id, string sectionKey, string displayName, int sortOrder)
    {
        NavigationMenuSection section = new NavigationMenuSection();
        section.Id = id;
        section.SectionKey = sectionKey;
        section.DisplayName = displayName;
        section.SortOrder = sortOrder;
        return section;
    }
}
