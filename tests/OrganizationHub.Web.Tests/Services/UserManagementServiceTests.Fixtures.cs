using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Infrastructure;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class UserManagementServiceTests
{
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
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser;
            userAccount.Team = planningTeam;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.Teams.Add(planningTeam);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return userAccount.Id;
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
            return managementTeam.Id;
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
            return team.Id;
        }
    }

    private async Task<int> createPositionAsync(TestDbContextFactory dbContextFactory, int teamId, string name)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition position = new OrganizationPosition();
            position.TeamId = teamId;
            position.Name = name;
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            dbContext.Positions.Add(position);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return position.Id;
        }
    }

    private async Task createAdminUserAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team systemTeam = new Team();
            systemTeam.TeamKey = "System";
            systemTeam.DisplayName = "시스템 관리";
            systemTeam.SortOrder = 0;
            systemTeam.IsActive = true;
            OrganizationPosition systemAdministratorPosition = new OrganizationPosition();
            systemAdministratorPosition.Team = systemTeam;
            systemAdministratorPosition.Name = "시스템 관리자";
            systemAdministratorPosition.Description = string.Empty;
            systemAdministratorPosition.SortOrder = 10;
            systemAdministratorPosition.IsActive = true;
            systemAdministratorPosition.IsSystemAdministrator = true;
            UserAccount userAccount = new UserAccount();
            userAccount.Id = 9;
            userAccount.Username = "site.admin";
            userAccount.DisplayName = "사이트 관리자";
            userAccount.PasswordHash = "test";
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.Admin;
            userAccount.Position = systemAdministratorPosition;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.Teams.Add(systemTeam);
            dbContext.Positions.Add(systemAdministratorPosition);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<int> createSystemAdministratorPositionUserAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition systemAdministratorPosition = await dbContext.Positions.FirstAsync(position => position.IsSystemAdministrator, CancellationToken.None);
            UserAccount userAccount = new UserAccount();
            userAccount.Id = 12;
            userAccount.Username = "compat.admin";
            userAccount.DisplayName = "호환 관리자";
            userAccount.PasswordHash = "test";
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser;
            userAccount.TeamId = systemAdministratorPosition.TeamId;
            userAccount.PositionId = systemAdministratorPosition.Id;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return userAccount.Id;
        }
    }

    private static UserSession createDirectorSession()
    {
        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(10),
                Username = Username.CreateOptional("director"),
                DisplayName = DisplayName.CreateOptional("디렉터"),
                StoredCompatibilityRole = EStoredCompatibilityRole.ManagementDirector,
                OrganizationTeamIdOrNull = null,
                TeamKey = OrganizationTeamKey.Empty,
                TeamDisplayName = DisplayName.CreateOptional(string.Empty)
            });
    }

    private static UserSession createAdminSession()
    {
        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(9),
                Username = Username.CreateOptional("admin"),
                DisplayName = DisplayName.CreateOptional("관리자"),
                StoredCompatibilityRole = EStoredCompatibilityRole.Admin,
                OrganizationTeamIdOrNull = null,
                TeamKey = OrganizationTeamKey.Empty,
                TeamDisplayName = DisplayName.CreateOptional(string.Empty)
            });
    }
}
