using System;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Tests.Infrastructure;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class PermissionServiceTests
{
    private static TestDbContextFactory createConfiguredDbContextFactory(EOfficialRootConfiguration officialRootConfiguration)
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.Id = 1;
            setting.PublicFolderId = "public-folder";
            setting.CommonFolderId = "common-folder";
            setting.ResponsibleResourcesFolderId = "responsible-folder";
            setting.WorkResourcesFolderId = "work-resources-folder";
            setting.ArchiveFolderId = "archive-folder";
            setting.OrganizationCalendarId = "management-calendar";
            setting.ResponsibleCalendarId = "responsible-calendar";
            if (officialRootConfiguration == EOfficialRootConfiguration.WithOfficialRoot)
            {
                setting.OfficialRootFolderId = "official-root";
            }

            Team planningTeam = createTeam(1, "Planning", "기획팀", "planning-materials-folder", "planning-approved-folder", "planning-calendar", 10);
            Team operationTeam = createTeam(2, "Operation", "운영팀", "operation-materials-folder", "operation-approved-folder", "operation-calendar", 20);
            Team managementTeam = createTeam(3, "Management", "관리팀", "management-folder", "management-approved-folder", string.Empty, 0);
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(managementTeam);
            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(operationTeam);
            seedPermissionUsers(dbContext);
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static void seedPermissionUsers(OrganizationHubDbContext dbContext)
    {
        OrganizationPosition planningMemberPosition = createPosition(1, "기획팀 구성원", 10, false);
        addPermissionGrant(planningMemberPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
        addPermissionGrant(planningMemberPosition, PermissionKeys.FILES_UPLOAD, EPermissionScopeType.OwnTeam);
        addPermissionGrant(planningMemberPosition, PermissionKeys.CALENDAR_READ, EPermissionScopeType.OwnTeam);

        OrganizationPosition planningLeaderPosition = createPosition(1, "기획팀 책임자", 20, false);
        addPermissionGrant(planningLeaderPosition, PermissionKeys.FILES_READ, EPermissionScopeType.Global);
        addPermissionGrant(planningLeaderPosition, PermissionKeys.FILES_UPLOAD, EPermissionScopeType.Global);
        addPermissionGrant(planningLeaderPosition, PermissionKeys.FILES_DELETE, EPermissionScopeType.OwnTeam);
        addPermissionGrant(planningLeaderPosition, PermissionKeys.CALENDAR_READ, EPermissionScopeType.Global);
        addPermissionGrant(planningLeaderPosition, PermissionKeys.CALENDAR_CREATE, EPermissionScopeType.Global);

        OrganizationPosition planningDeputyPosition = createPosition(1, "기획팀 부책임자", 30, false);
        addPermissionGrant(planningDeputyPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
        addPermissionGrant(planningDeputyPosition, PermissionKeys.FILES_UPLOAD, EPermissionScopeType.OwnTeam);
        addPermissionGrant(planningDeputyPosition, PermissionKeys.CALENDAR_READ, EPermissionScopeType.OwnTeam);

        OrganizationPosition operationLeaderPosition = createPosition(2, "운영팀 책임자", 20, false);
        addPermissionGrant(operationLeaderPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
        addPermissionGrant(operationLeaderPosition, PermissionKeys.FILES_UPLOAD, EPermissionScopeType.OwnTeam);
        addPermissionGrant(operationLeaderPosition, PermissionKeys.FILES_DELETE, EPermissionScopeType.OwnTeam);

        OrganizationPosition managementStaffPosition = createPosition(3, "관리팀 담당자", 10, false);
        addPermissionGrant(managementStaffPosition, PermissionKeys.FILES_READ, EPermissionScopeType.Global);
        addPermissionGrant(managementStaffPosition, PermissionKeys.FILES_UPLOAD, EPermissionScopeType.Global);
        addPermissionGrant(managementStaffPosition, PermissionKeys.CALENDAR_READ, EPermissionScopeType.Global);
        addPermissionGrant(managementStaffPosition, PermissionKeys.CALENDAR_CREATE, EPermissionScopeType.Global);

        OrganizationPosition managementDirectorPosition = createPosition(3, "관리 책임자", 20, false);
        addPermissionGrant(managementDirectorPosition, PermissionKeys.FILES_READ, EPermissionScopeType.Global);
        addPermissionGrant(managementDirectorPosition, PermissionKeys.FILES_UPLOAD, EPermissionScopeType.Global);
        addPermissionGrant(managementDirectorPosition, PermissionKeys.FILES_DELETE, EPermissionScopeType.Global);
        addPermissionGrant(managementDirectorPosition, PermissionKeys.CALENDAR_READ, EPermissionScopeType.Global);
        addPermissionGrant(managementDirectorPosition, PermissionKeys.CALENDAR_CREATE, EPermissionScopeType.Global);

        OrganizationPosition managementDeputyPosition = createPosition(3, "관리 부책임자", 30, false);
        addPermissionGrant(managementDeputyPosition, PermissionKeys.FILES_READ, EPermissionScopeType.Global);
        addPermissionGrant(managementDeputyPosition, PermissionKeys.FILES_DELETE, EPermissionScopeType.Global);

        OrganizationPosition managementManagerPosition = createPosition(3, "관리 매니저", 40, false);
        addPermissionGrant(managementManagerPosition, PermissionKeys.FILES_READ, EPermissionScopeType.Global);
        addPermissionGrant(managementManagerPosition, PermissionKeys.FILES_DELETE, EPermissionScopeType.Global);

        OrganizationPosition systemAdministratorPosition = createPosition(3, "시스템 관리자", 50, true);

        dbContext.Positions.Add(planningMemberPosition);
        dbContext.Positions.Add(planningLeaderPosition);
        dbContext.Positions.Add(planningDeputyPosition);
        dbContext.Positions.Add(operationLeaderPosition);
        dbContext.Positions.Add(managementStaffPosition);
        dbContext.Positions.Add(managementDirectorPosition);
        dbContext.Positions.Add(managementDeputyPosition);
        dbContext.Positions.Add(managementManagerPosition);
        dbContext.Positions.Add(systemAdministratorPosition);
        dbContext.UserAccounts.Add(createUser(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1, planningMemberPosition));
        dbContext.UserAccounts.Add(createUser(11, "planning.leader", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1, planningLeaderPosition));
        dbContext.UserAccounts.Add(createUser(12, "planning.deputy", "기획 부책임자", EStoredCompatibilityRole.TeamDeputyLeader, 1, planningDeputyPosition));
        dbContext.UserAccounts.Add(createUser(4, "operation.leader", "운영 책임자", EStoredCompatibilityRole.TeamLeader, 2, operationLeaderPosition));
        dbContext.UserAccounts.Add(createUser(2, "management.staff", "관리팀 담당자", EStoredCompatibilityRole.ManagementStaff, 3, managementStaffPosition));
        dbContext.UserAccounts.Add(createUser(3, "director", "Director", EStoredCompatibilityRole.ManagementDirector, 3, managementDirectorPosition));
        dbContext.UserAccounts.Add(createUser(5, "deputy.director", "Deputy Director", EStoredCompatibilityRole.ManagementDeputyDirector, 3, managementDeputyPosition));
        dbContext.UserAccounts.Add(createUser(6, "manager", "Manager", EStoredCompatibilityRole.ManagementManager, 3, managementManagerPosition));
        dbContext.UserAccounts.Add(createUser(7, "admin", "Admin", EStoredCompatibilityRole.Admin, 3, systemAdministratorPosition));
    }

    private static OrganizationPosition createPosition(int teamId, string name, int sortOrder, bool isSystemAdministrator)
    {
        OrganizationPosition position = new OrganizationPosition();
        position.TeamId = teamId;
        position.Name = name;
        position.Description = string.Empty;
        position.SortOrder = sortOrder;
        position.IsActive = true;
        position.IsSystemAdministrator = isSystemAdministrator;
        return position;
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

    private static UserAccount createUser(int id, string username, string displayName, EStoredCompatibilityRole role, int teamId, OrganizationPosition position)
    {
        UserAccount userAccount = new UserAccount();
        userAccount.Id = id;
        userAccount.Username = username;
        userAccount.DisplayName = displayName;
        userAccount.PasswordHash = "test";
        userAccount.StoredCompatibilityRole = role;
        userAccount.TeamId = teamId;
        userAccount.Position = position;
        userAccount.IsActive = true;
        userAccount.CreatedAt = DateTime.UtcNow;
        return userAccount;
    }

    private static Team createTeam(int id, string teamKey, string displayName, string materialsFolderId, string approvedFolderId, string calendarId, int sortOrder)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.MaterialsFolderId = materialsFolderId;
        team.ApprovedFolderId = approvedFolderId;
        team.CalendarId = calendarId;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private enum EOfficialRootConfiguration
    {
        WithoutOfficialRoot,
        WithOfficialRoot
    }
}
