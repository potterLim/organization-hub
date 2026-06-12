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
            PermissionAuthorizationRequirement requirement = new PermissionAuthorizationRequirement(new[] { PermissionKeys.USERS_MANAGE }, new List<string>());
            AuthorizationHandlerContext context = new AuthorizationHandlerContext(new[] { requirement }, createPrincipal(1, EUserRole.TeamUser, 1), null);

            await handler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }
    }

    [Fact]
    public async Task FallbackRoleSatisfiesRequirementWhenUserHasNoConfiguredPositionAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedUserWithoutPositionAsync(dbContextFactory);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);
            PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(permissionService);
            PermissionAuthorizationRequirement requirement = new PermissionAuthorizationRequirement(new[] { PermissionKeys.USERS_MANAGE }, new[] { OrganizationHubRoles.ADMIN });
            AuthorizationHandlerContext context = new AuthorizationHandlerContext(new[] { requirement }, createPrincipal(2, EUserRole.Admin, null), null);

            await handler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }
    }

    private static async Task seedConfiguredUserAsync(TestDbContextFactory dbContextFactory, string permissionKey)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team team = new Team();
            team.ID = 1;
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
            permissionGrant.ScopeType = EPermissionScopeType.Global;
            permissionGrant.IsAllowed = true;
            UserAccount userAccount = new UserAccount();
            userAccount.ID = 1;
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
            userAccount.ID = 2;
            userAccount.Username = "admin";
            userAccount.DisplayName = "관리자";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.Admin;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static ClaimsPrincipal createPrincipal(int userID, EUserRole role, int? teamIDOrNull)
    {
        List<Claim> claims = new List<Claim>();
        claims.Add(new Claim(OrganizationHubClaimTypes.USER_ID, userID.ToString()));
        claims.Add(new Claim(OrganizationHubClaimTypes.USERNAME, "user" + userID.ToString()));
        claims.Add(new Claim(OrganizationHubClaimTypes.DISPLAY_NAME, "User " + userID.ToString()));
        claims.Add(new Claim(OrganizationHubClaimTypes.ROLE, role.ToString()));
        claims.Add(new Claim(ClaimTypes.Role, UserRoleUtility.GetRoleName(role)));
        if (teamIDOrNull.HasValue)
        {
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_ID, teamIDOrNull.Value.ToString()));
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_KEY, "Planning"));
            claims.Add(new Claim(OrganizationHubClaimTypes.TEAM_DISPLAY_NAME, "기획팀"));
        }

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}
