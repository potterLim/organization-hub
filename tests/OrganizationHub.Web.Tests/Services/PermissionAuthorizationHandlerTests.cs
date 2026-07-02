using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task ConfiguredPermissionSatisfiesRequirementAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedConfiguredUserAsync(dbContextFactory, PermissionKeys.USERS_MANAGE);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(permissionService);
            PermissionAuthorizationRequirement requirement = new PermissionAuthorizationRequirement(new PermissionKey[] { new PermissionKey(PermissionKeys.USERS_MANAGE) });
            AuthorizationHandlerContext context = new AuthorizationHandlerContext(new PermissionAuthorizationRequirement[] { requirement }, createPrincipal(1, EStoredCompatibilityRole.TeamUser, 1), null);

            await handler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }
    }

    [Fact]
    public async Task UserWithoutPositionDoesNotSatisfyRequirementAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedUserWithoutPositionAsync(dbContextFactory);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(permissionService);
            PermissionAuthorizationRequirement requirement = new PermissionAuthorizationRequirement(new PermissionKey[] { new PermissionKey(PermissionKeys.USERS_MANAGE) });
            AuthorizationHandlerContext context = new AuthorizationHandlerContext(new PermissionAuthorizationRequirement[] { requirement }, createPrincipal(2, EStoredCompatibilityRole.Admin, null), null);

            await handler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }
    }

    [Fact]
    public async Task SystemAdministratorPositionSatisfiesRequirementAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedConfiguredAdministratorAsync(dbContextFactory);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(permissionService);
            PermissionAuthorizationRequirement requirement = new PermissionAuthorizationRequirement(new PermissionKey[] { new PermissionKey(PermissionKeys.GOOGLE_CONNECTION_MANAGE) });
            AuthorizationHandlerContext context = new AuthorizationHandlerContext(new PermissionAuthorizationRequirement[] { requirement }, createPrincipal(3, EStoredCompatibilityRole.Admin, null), null);

            await handler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }
    }

    [Fact]
    public async Task ConfiguredOwnTeamPermissionSatisfiesUserTeamRequirementAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedConfiguredUserAsync(dbContextFactory, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(permissionService);
            PermissionAuthorizationRequirement requirement = new PermissionAuthorizationRequirement(
                new PermissionKey[] { new PermissionKey(PermissionKeys.FILES_READ) },
                EPermissionAuthorizationTarget.UserTeam
            );
            AuthorizationHandlerContext context = new AuthorizationHandlerContext(new PermissionAuthorizationRequirement[] { requirement }, createPrincipal(1, EStoredCompatibilityRole.TeamLeader, 1), null);

            await handler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }
    }

    private static async Task seedConfiguredUserAsync(
        TestDbContextFactory dbContextFactory,
        string permissionKey,
        EPermissionScopeType scopeType = EPermissionScopeType.Global
    )
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
            PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
            permissionGrant.Position = position;
            permissionGrant.PermissionKey = permissionKey;
            permissionGrant.ScopeType = scopeType;
            permissionGrant.IsAllowed = true;
            UserAccount userAccount = new UserAccount();
            userAccount.Id = 1;
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
            dbContext.PositionPermissionGrants.Add(permissionGrant);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static async Task seedUserWithoutPositionAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            UserAccount userAccount = new UserAccount();
            userAccount.Id = 2;
            userAccount.Username = "admin";
            userAccount.DisplayName = "관리자";
            userAccount.PasswordHash = "test";
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.Admin;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static async Task seedConfiguredAdministratorAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team team = new Team();
            team.Id = 3;
            team.TeamKey = "Administration";
            team.DisplayName = "관리팀";
            team.IsActive = true;
            OrganizationPosition position = new OrganizationPosition();
            position.Team = team;
            position.Name = "일반 직책";
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            position.IsSystemAdministrator = true;
            UserAccount userAccount = new UserAccount();
            userAccount.Id = 3;
            userAccount.Username = "configured.admin";
            userAccount.DisplayName = "설정형 관리자";
            userAccount.PasswordHash = "test";
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.Admin;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.Teams.Add(team);
            dbContext.Positions.Add(position);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static ClaimsPrincipal createPrincipal(int userId, EStoredCompatibilityRole role, int? teamIdOrNull)
    {
        List<Claim> claims = new List<Claim>();
        claims.Add(new Claim(OrganizationHubClaimTypes.USER_ID, userId.ToString()));
        claims.Add(new Claim(OrganizationHubClaimTypes.USERNAME, "user" + userId.ToString()));
        claims.Add(new Claim(OrganizationHubClaimTypes.DISPLAY_NAME, "User " + userId.ToString()));
        claims.Add(new Claim(OrganizationHubClaimTypes.ROLE, role.ToString()));
        if (teamIdOrNull.HasValue)
        {
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_ID, teamIdOrNull.Value.ToString()));
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_KEY, "Planning"));
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_DISPLAY_NAME, "기획팀"));
        }

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}
