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

public sealed partial class UserManagementServiceTests
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
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
            formModel.TeamId = null;
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
            int userId = await createPlanningUserAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Id = userId;
            formModel.Username = "planning.member";
            formModel.DisplayName = "기획 운영";
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
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
            int managementTeamId = await createManagementTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "management.office";
            formModel.DisplayName = "관리팀 지원";
            formModel.Password = "ChangeMe!2026";
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
            formModel.TeamId = managementTeamId;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.Include(user => user.Team).FirstOrDefaultAsync(user => user.Username == "management.office", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EStoredCompatibilityRole.TeamUser, userAccountOrNull.StoredCompatibilityRole);
                Assert.NotNull(userAccountOrNull.Team);
                if (userAccountOrNull.Team != null)
                {
                    Assert.Equal("Management", userAccountOrNull.Team.TeamKey);
                }
            }
        }
    }

    [Fact]
    public async Task NonAdminUserManagerCreatesOrganizationMemberWithDefaultStoredCompatibilityRoleAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int managementTeamId = await createManagementTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "new.director";
            formModel.DisplayName = "신규 디렉터";
            formModel.Password = "ChangeMe!2026";
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
            formModel.TeamId = managementTeamId;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Username == "new.director", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EStoredCompatibilityRole.TeamUser, userAccountOrNull.StoredCompatibilityRole);
            }
        }
    }

    [Fact]
    public async Task SystemAdministratorUsesConfiguredPositionsForOrganizationMembersAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int managementTeamId = await createManagementTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "new.director";
            formModel.DisplayName = "신규 디렉터";
            formModel.Password = "ChangeMe!2026";
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
            formModel.TeamId = managementTeamId;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createAdminSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Username == "new.director", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EStoredCompatibilityRole.TeamUser, userAccountOrNull.StoredCompatibilityRole);
            }
        }
    }

    [Fact]
    public async Task CreateOrganizationMemberStoresStandardCompatibilityRoleAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int planningTeamId = await createPlanningTeamAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "planning.deputy";
            formModel.DisplayName = "기획 부책임자";
            formModel.Password = "ChangeMe!2026";
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
            formModel.TeamId = planningTeamId;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.Include(user => user.Team).FirstOrDefaultAsync(user => user.Username == "planning.deputy", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(EStoredCompatibilityRole.TeamUser, userAccountOrNull.StoredCompatibilityRole);
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
            int planningTeamId = await createPlanningTeamAsync(dbContextFactory);
            int managerPositionId = await createPositionAsync(dbContextFactory, planningTeamId, "문서 관리자");
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "planning.docs";
            formModel.DisplayName = "문서 담당";
            formModel.Password = "ChangeMe!2026";
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
            formModel.TeamId = planningTeamId;
            formModel.PositionId = managerPositionId;
            formModel.IsActive = true;

            ServiceResult result = await userManagementService.CreateUserAsync(createDirectorSession(), formModel, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                UserAccount? userAccountOrNull = await dbContext.UserAccounts.Include(user => user.Position).FirstOrDefaultAsync(user => user.Username == "planning.docs", CancellationToken.None);
                Assert.NotNull(userAccountOrNull);
                Assert.Equal(managerPositionId, userAccountOrNull.PositionId);
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
            int planningTeamId = await createPlanningTeamAsync(dbContextFactory);
            int operationTeamId = await createTeamAsync(dbContextFactory, "Operation", "운영팀", 20);
            int operationPositionId = await createPositionAsync(dbContextFactory, operationTeamId, "운영 담당");
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());
            UserAccountFormModel formModel = new UserAccountFormModel();
            formModel.Username = "planning.invalid";
            formModel.DisplayName = "잘못된 직책";
            formModel.Password = "ChangeMe!2026";
            formModel.AccountKind = EUserAccountKind.OrganizationMember;
            formModel.TeamId = planningTeamId;
            formModel.PositionId = operationPositionId;
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
            UserSession managerSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(11),
                    Username = Username.CreateOptional("manager"),
                    DisplayName = DisplayName.CreateOptional("매니저"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementManager,
                    OrganizationTeamIdOrNull = null,
                    TeamKey = OrganizationTeamKey.Empty,
                    TeamDisplayName = DisplayName.CreateOptional(string.Empty)
                });
            UserAccountFormModel adminFormModel = new UserAccountFormModel();
            adminFormModel.Username = "site.admin2";
            adminFormModel.DisplayName = "사이트 관리자2";
            adminFormModel.Password = "ChangeMe!2026";
            adminFormModel.AccountKind = EUserAccountKind.SystemAdministrator;
            adminFormModel.IsActive = true;

            IReadOnlyList<UserAccount> adminUsers = await userManagementService.GetUsersAsync(adminSession, CancellationToken.None);
            IReadOnlyList<UserAccount> managerUsers = await userManagementService.GetUsersAsync(managerSession, CancellationToken.None);
            ServiceResult managerCreateResult = await userManagementService.CreateUserAsync(managerSession, adminFormModel, CancellationToken.None);
            ServiceResult adminCreateResult = await userManagementService.CreateUserAsync(adminSession, adminFormModel, CancellationToken.None);

            Assert.Contains(adminUsers, user => user.StoredCompatibilityRole == EStoredCompatibilityRole.Admin);
            Assert.DoesNotContain(managerUsers, user => user.StoredCompatibilityRole == EStoredCompatibilityRole.Admin);
            Assert.False(managerCreateResult.IsSuccess);
            Assert.True(adminCreateResult.IsSuccess);
        }
    }

    [Fact]
    public async Task SystemAdministratorPositionProtectsCompatibilityRoleUserAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await createAdminUserAsync(dbContextFactory);
            int protectedUserId = await createSystemAdministratorPositionUserAsync(dbContextFactory);
            UserManagementService userManagementService = new UserManagementService(dbContextFactory, new Pbkdf2PasswordHashService());

            ServiceResult managerResult = await userManagementService.DeactivateUserAsync(createDirectorSession(), UserAccountId.CreateExisting(protectedUserId), CancellationToken.None);
            ServiceResult adminResult = await userManagementService.DeactivateUserAsync(createAdminSession(), UserAccountId.CreateExisting(protectedUserId), CancellationToken.None);

            Assert.False(managerResult.IsSuccess);
            Assert.True(adminResult.IsSuccess);
        }
    }

}
