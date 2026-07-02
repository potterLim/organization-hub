using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Infrastructure;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class PermissionServiceTests
{
    private static async Task<UserSession> seedConfiguredPermissionUserAsync(TestDbContextFactory dbContextFactory, int userId, int teamId, EPermissionScopeType scopeType, IReadOnlyList<string> permissionKeys)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition position = new OrganizationPosition();
            position.TeamId = teamId;
            position.Name = "자료 담당";
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            dbContext.Positions.Add(position);

            foreach (string permissionKey in permissionKeys)
            {
                PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
                permissionGrant.Position = position;
                permissionGrant.PermissionKey = permissionKey;
                permissionGrant.ScopeType = scopeType;
                permissionGrant.IsAllowed = true;
                dbContext.PositionPermissionGrants.Add(permissionGrant);
            }

            UserAccount userAccount = new UserAccount();
            userAccount.Id = userId;
            userAccount.Username = "configured.user";
            userAccount.DisplayName = "권한 설정 사용자";
            userAccount.PasswordHash = "test";
            userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser;
            userAccount.TeamId = teamId;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(userAccount.Id),
                    Username = Username.CreateOptional(userAccount.Username),
                    DisplayName = DisplayName.CreateOptional(userAccount.DisplayName),
                    StoredCompatibilityRole = userAccount.StoredCompatibilityRole,
                    OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(teamId),
                    TeamKey = new OrganizationTeamKey("Planning"),
                    TeamDisplayName = DisplayName.CreateOptional("기획팀")
                });
        }
    }

    private static UserSession createPlanningUserSession(EStoredCompatibilityRole role)
    {
        switch (role)
        {
            case EStoredCompatibilityRole.TeamLeader:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(11),
                        Username = Username.CreateOptional("planning.leader"),
                        DisplayName = DisplayName.CreateOptional("기획 책임자"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                        TeamKey = new OrganizationTeamKey("Planning"),
                        TeamDisplayName = DisplayName.CreateOptional("기획팀")
                    });
            case EStoredCompatibilityRole.TeamDeputyLeader:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(12),
                        Username = Username.CreateOptional("planning.deputy"),
                        DisplayName = DisplayName.CreateOptional("기획 부책임자"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                        TeamKey = new OrganizationTeamKey("Planning"),
                        TeamDisplayName = DisplayName.CreateOptional("기획팀")
                    });
            default:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(1),
                        Username = Username.CreateOptional("planning.member"),
                        DisplayName = DisplayName.CreateOptional("기획팀원"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                        TeamKey = new OrganizationTeamKey("Planning"),
                        TeamDisplayName = DisplayName.CreateOptional("기획팀")
                    });
        }
    }

    private static UserSession createManagementSession(EStoredCompatibilityRole role)
    {
        switch (role)
        {
            case EStoredCompatibilityRole.ManagementDirector:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(3),
                        Username = Username.CreateOptional("director"),
                        DisplayName = DisplayName.CreateOptional("Director"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                        TeamKey = new OrganizationTeamKey("Management"),
                        TeamDisplayName = DisplayName.CreateOptional("관리팀")
                    });
            case EStoredCompatibilityRole.ManagementDeputyDirector:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(5),
                        Username = Username.CreateOptional("deputy.director"),
                        DisplayName = DisplayName.CreateOptional("Deputy Director"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                        TeamKey = new OrganizationTeamKey("Management"),
                        TeamDisplayName = DisplayName.CreateOptional("관리팀")
                    });
            case EStoredCompatibilityRole.ManagementManager:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(6),
                        Username = Username.CreateOptional("manager"),
                        DisplayName = DisplayName.CreateOptional("Manager"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                        TeamKey = new OrganizationTeamKey("Management"),
                        TeamDisplayName = DisplayName.CreateOptional("관리팀")
                    });
            case EStoredCompatibilityRole.Admin:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(7),
                        Username = Username.CreateOptional("admin"),
                        DisplayName = DisplayName.CreateOptional("Admin"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                        TeamKey = new OrganizationTeamKey("Management"),
                        TeamDisplayName = DisplayName.CreateOptional("관리팀")
                    });
            default:
                return new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(2),
                        Username = Username.CreateOptional("management.staff"),
                        DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                        StoredCompatibilityRole = role,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(3),
                        TeamKey = new OrganizationTeamKey("Management"),
                        TeamDisplayName = DisplayName.CreateOptional("관리팀")
                    });
        }
    }
}
