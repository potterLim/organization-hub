using System;
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

public sealed class OrganizationPermissionServiceTests
{
    [Fact]
    public async Task GlobalGrantAllowsAnyTargetTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            SeededUser seededUser = await seedUserWithGrantAsync(dbContextFactory, EPermissionScopeType.Global, null);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);

            PermissionDecision decision = await permissionService.DecidePermissionAsync(seededUser.Session, new PermissionKey(PermissionKeys.FILES_UPLOAD), 999, CancellationToken.None);

            Assert.True(decision.IsAllowed);
        }
    }

    [Fact]
    public async Task OwnTeamGrantAllowsOnlyUsersTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            SeededUser seededUser = await seedUserWithGrantAsync(dbContextFactory, EPermissionScopeType.OwnTeam, null);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);

            PermissionDecision ownTeamDecision = await permissionService.DecidePermissionAsync(seededUser.Session, new PermissionKey(PermissionKeys.FILES_UPLOAD), seededUser.TeamID, CancellationToken.None);
            PermissionDecision otherTeamDecision = await permissionService.DecidePermissionAsync(seededUser.Session, new PermissionKey(PermissionKeys.FILES_UPLOAD), seededUser.TeamID + 100, CancellationToken.None);

            Assert.True(ownTeamDecision.IsAllowed);
            Assert.False(otherTeamDecision.IsAllowed);
        }
    }

    [Fact]
    public async Task SelectedTeamGrantAllowsConfiguredTargetTeamAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int targetTeamID = await seedTeamAsync(dbContextFactory, "Archive", "보관팀", 30);
            SeededUser seededUser = await seedUserWithGrantAsync(dbContextFactory, EPermissionScopeType.SelectedTeam, targetTeamID);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);

            PermissionDecision targetTeamDecision = await permissionService.DecidePermissionAsync(seededUser.Session, new PermissionKey(PermissionKeys.FILES_UPLOAD), targetTeamID, CancellationToken.None);
            PermissionDecision ownTeamDecision = await permissionService.DecidePermissionAsync(seededUser.Session, new PermissionKey(PermissionKeys.FILES_UPLOAD), seededUser.TeamID, CancellationToken.None);

            Assert.True(targetTeamDecision.IsAllowed);
            Assert.False(ownTeamDecision.IsAllowed);
        }
    }

    [Fact]
    public async Task UserWithoutPositionIsDeniedAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            int teamID = await seedTeamAsync(dbContextFactory, "Planning", "기획팀", 10);
            UserSession userSession = await seedUserWithoutPositionAsync(dbContextFactory, teamID);
            OrganizationPermissionService permissionService = new OrganizationPermissionService(dbContextFactory);

            PermissionDecision decision = await permissionService.DecidePermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_UPLOAD), teamID, CancellationToken.None);

            Assert.False(decision.IsAllowed);
        }
    }

    private static async Task<SeededUser> seedUserWithGrantAsync(TestDbContextFactory dbContextFactory, EPermissionScopeType scopeType, int? targetTeamIDOrNull)
    {
        int teamID = await seedTeamAsync(dbContextFactory, "Planning", "기획팀", 10);
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition position = new OrganizationPosition();
            position.TeamID = teamID;
            position.Name = "자료 담당";
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
            permissionGrant.Position = position;
            permissionGrant.PermissionKey = PermissionKeys.FILES_UPLOAD;
            permissionGrant.ScopeType = scopeType;
            permissionGrant.TargetTeamID = targetTeamIDOrNull;
            permissionGrant.IsAllowed = true;
            UserAccount userAccount = new UserAccount();
            userAccount.Username = "planning.member";
            userAccount.DisplayName = "기획 담당";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.TeamUser;
            userAccount.TeamID = teamID;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.Positions.Add(position);
            dbContext.PositionPermissionGrants.Add(permissionGrant);
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            UserSession session = new UserSession(userAccount.ID, userAccount.Username, userAccount.DisplayName, userAccount.Role, teamID, "Planning", "기획팀");
            return new SeededUser(session, teamID);
        }
    }

    private static async Task<UserSession> seedUserWithoutPositionAsync(TestDbContextFactory dbContextFactory, int teamID)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            UserAccount userAccount = new UserAccount();
            userAccount.Username = "planning.unassigned";
            userAccount.DisplayName = "미배정";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.TeamUser;
            userAccount.TeamID = teamID;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new UserSession(userAccount.ID, userAccount.Username, userAccount.DisplayName, userAccount.Role, teamID, "Planning", "기획팀");
        }
    }

    private static async Task<int> seedTeamAsync(TestDbContextFactory dbContextFactory, string teamKey, string displayName, int sortOrder)
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

    private sealed class SeededUser
    {
        public UserSession Session
        {
            get; private init;
        }
        public int TeamID
        {
            get; private init;
        }

        public SeededUser(UserSession session, int teamID)
        {
            Session = session;
            TeamID = teamID;
        }
    }
}
