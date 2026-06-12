using System;
using System.Collections.Generic;
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

public sealed class UserManagementServiceTests
{
    [Fact]
    public async Task CreateNonAdministratorUserRequiresTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "management.user";
            formModel.DisplayName = "운영 담당";
            formModel.Password = "ChangeMe!2026";
            formModel.Role = EUserRole.ManagementStaff;
            formModel.TeamID = null;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("소속 팀", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task UpdateNonAdministratorUserRequiresTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int userID = await createPlanningUserAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.ID = userID;
            formModel.Username = "planning.member";
            formModel.DisplayName = "기획 운영";
            formModel.Role = EUserRole.ManagementRecorder;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.UpdateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains("소속 팀", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task CreateUserWithManagementTeamKeepsDefaultOrganizationMemberRoleAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int managementTeamID = await createManagementTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "management.office";
            formModel.DisplayName = "관리팀 지원";
            formModel.Password = "ChangeMe!2026";
            formModel.Role = EUserRole.TeamUser;
            formModel.TeamID = managementTeamID;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.Include(user => user.Team).FirstOrDefaultAsync(user => user.Username == "management.office", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EUserRole.TeamUser, userAccountOrNull.Role);
                Assert.NotNull(userAccountOrNull.Team);
                if (userAccountOrNull.Team != null)
                {
                    Assert.Equal("Management", userAccountOrNull.Team.TeamKey);
                }
            }
        }
    }

    [Fact]
    public async Task NonAdminUserManagerCreatesManagementUserWithDefaultRoleAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int managementTeamID = await createManagementTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "new.director";
            formModel.DisplayName = "신규 디렉터";
            formModel.Password = "ChangeMe!2026";
            formModel.Role = EUserRole.ManagementDirector;
            formModel.TeamID = managementTeamID;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Username == "new.director", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EUserRole.TeamUser, userAccountOrNull.Role);
            }
        }
    }

    [Fact]
    public async Task SystemAdministratorUsesConfiguredPositionsInsteadOfManagementRoleAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int managementTeamID = await createManagementTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "new.director";
            formModel.DisplayName = "신규 디렉터";
            formModel.Password = "ChangeMe!2026";
            formModel.Role = EUserRole.ManagementDirector;
            formModel.TeamID = managementTeamID;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createAdminSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Username == "new.director", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EUserRole.TeamUser, userAccountOrNull.Role);
            }
        }
    }

    [Fact]
    public async Task CreateTeamDeputyLeaderKeepsTeamAffiliationAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int planningTeamID = await createPlanningTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "planning.deputy";
            formModel.DisplayName = "기획 부책임자";
            formModel.Password = "ChangeMe!2026";
            formModel.Role = EUserRole.TeamDeputyLeader;
            formModel.TeamID = planningTeamID;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.Include(user => user.Team).FirstOrDefaultAsync(user => user.Username == "planning.deputy", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EUserRole.TeamDeputyLeader, userAccountOrNull.Role);
                Assert.NotNull(userAccountOrNull.Team);
                if (userAccountOrNull.Team != null)
                {
                    Assert.Equal("Planning", userAccountOrNull.Team.TeamKey);
                }
            }
        }
    }

    [Fact]
    public async Task CreateUserStoresSelectedOrganizationPositionAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int planningTeamID = await createPlanningTeamAsync(dbContextFactory);
            int managerPositionID = await createPositionAsync(dbContextFactory, planningTeamID, "문서 관리자");
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "planning.docs";
            formModel.DisplayName = "문서 담당";
            formModel.Password = "ChangeMe!2026";
            formModel.Role = EUserRole.TeamUser;
            formModel.TeamID = planningTeamID;
            formModel.PositionID = managerPositionID;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.Include(user => user.Position).FirstOrDefaultAsync(user => user.Username == "planning.docs", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(managerPositionID, userAccountOrNull.PositionID);
                Assert.NotNull(userAccountOrNull.Position);
                if (userAccountOrNull.Position != null)
                {
                    Assert.Equal("문서 관리자", userAccountOrNull.Position.Name);
                }
            }
        }
    }

    [Fact]
    public async Task CreateUserRejectsPositionFromDifferentTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int planningTeamID = await createPlanningTeamAsync(dbContextFactory);
            int operationTeamID = await createTeamAsync(dbContextFactory, "Operation", "운영팀", 20);
            int operationPositionID = await createPositionAsync(dbContextFactory, operationTeamID, "운영 담당");
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "planning.invalid";
            formModel.DisplayName = "잘못된 직책";
            formModel.Password = "ChangeMe!2026";
            formModel.Role = EUserRole.TeamUser;
            formModel.TeamID = planningTeamID;
            formModel.PositionID = operationPositionID;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.False(result.IsSuccess);
        }
    }

    [Fact]
    public async Task SystemAdministratorCanSeeAndCreateSystemAdministratorsAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await createAdminUserAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserSession adminSession = createAdminSession();
            UserSession managerSession = new UserSession(11, "manager", "매니저", EUserRole.ManagementManager, null, string.Empty, string.Empty);
            UserAccountFormModel adminFormModel = new UserAccountFormModel();
            adminFormModel.Username = "site.admin2";
            adminFormModel.DisplayName = "사이트 관리자2";
            adminFormModel.Password = "ChangeMe!2026";
            adminFormModel.Role = EUserRole.Admin;
            adminFormModel.IsActive = true;

            IReadOnlyList<UserAccount> adminUsers = await userManagementService.GetUsersAsync(adminSession, CancellationToken.None);
            IReadOnlyList<UserAccount> managerUsers = await userManagementService.GetUsersAsync(managerSession, CancellationToken.None);
            ServiceResult managerCreateResult = await userManagementService.CreateUserAsync(managerSession, adminFormModel, CancellationToken.None);
            ServiceResult adminCreateResult = await userManagementService.CreateUserAsync(adminSession, adminFormModel, CancellationToken.None);

            Assert.Contains(adminUsers, user => user.Role == EUserRole.Admin);
            Assert.DoesNotContain(managerUsers, user => user.Role == EUserRole.Admin);
            Assert.False(managerCreateResult.IsSuccess);
            Assert.True(adminCreateResult.IsSuccess);
        }
    }

    private async Task<int> createPlanningUserAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team planningTeam = new Team();
            planningTeam.TeamKey = "Planning";
            planningTeam.DisplayName = "기획팀";
            planningTeam.SortOrder = 10;
            planningTeam.IsActive = true;
            UserAccount userAccount = new UserAccount();
            userAccount.Username = "planning.member";
            userAccount.DisplayName = "기획 운영";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.TeamUser;
            userAccount.Team = planningTeam;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.Teams.Add(planningTeam);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return userAccount.ID;
        }
    }

    private async Task<int> createManagementTeamAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team managementTeam = new Team();
            managementTeam.TeamKey = "Management";
            managementTeam.DisplayName = "관리팀";
            managementTeam.SortOrder = 0;
            managementTeam.IsActive = true;
            dbContext.Teams.Add(managementTeam);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return managementTeam.ID;
        }
    }

    private async Task<int> createPlanningTeamAsync(TestDbContextFactory dbContextFactory)
    {
        return await createTeamAsync(dbContextFactory, "Planning", "기획팀", 10);
    }

    private async Task<int> createTeamAsync(TestDbContextFactory dbContextFactory, string teamKey, string displayName, int sortOrder)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team team = new Team();
            team.TeamKey = teamKey;
            team.DisplayName = displayName;
            team.SortOrder = sortOrder;
            team.IsActive = true;
            dbContext.Teams.Add(team);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return team.ID;
        }
    }

    private async Task<int> createPositionAsync(TestDbContextFactory dbContextFactory, int teamID, string name)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition position = new OrganizationPosition();
            position.TeamID = teamID;
            position.Name = name;
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            dbContext.Positions.Add(position);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return position.ID;
        }
    }

    private async Task createAdminUserAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            UserAccount userAccount = new UserAccount();
            userAccount.Username = "site.admin";
            userAccount.DisplayName = "사이트 관리자";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.Admin;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static UserSession createDirectorSession()
    {
        return new UserSession(10, "director", "디렉터", EUserRole.ManagementDirector, null, string.Empty, string.Empty);
    }

    private static UserSession createAdminSession()
    {
        return new UserSession(9, "admin", "관리자", EUserRole.Admin, null, string.Empty, string.Empty);
    }
}
