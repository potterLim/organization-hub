using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class OrganizationPermissionService : IOrganizationPermissionService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    public OrganizationPermissionService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<bool> UsesConfiguredPermissionsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            bool hasConfiguredPosition = await dbContext.UserAccounts
                .Include(userAccount => userAccount.Position)
                .AnyAsync(userAccount =>
                    userAccount.Id == userSession.UserId &&
                    userAccount.IsActive &&
                    userAccount.Position != null &&
                    userAccount.Position.IsActive,
                    cancellationToken);
            return hasConfiguredPosition;
        }
    }

    public async Task<bool> HasPermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        CancellationToken cancellationToken
    )
    {
        PermissionDecision decision = await DecidePermissionAsync(userSession, permissionKey, targetTeamIdOrNull, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecidePermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        CancellationToken cancellationToken
    )
    {
        return await DecidePermissionAsync(userSession, permissionKey, targetTeamIdOrNull, null, cancellationToken);
    }

    public async Task<PermissionDecision> DecidePermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        UserAccountId? ownerUserIdOrNull,
        CancellationToken cancellationToken
    )
    {
        string requiredPermissionKey = permissionKey.GetRequiredValue();
        if (PermissionCatalog.GetDefinitions().Any(definition => definition.Key == requiredPermissionKey) == false)
        {
            return PermissionDecision.Deny("알 수 없는 권한입니다.", "권한");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UserAccount? userAccountOrNull = await loadActiveUserAccountOrNullAsync(dbContext, userSession, cancellationToken);
            if (userAccountOrNull == null)
            {
                return PermissionDecision.Deny("활성 사용자를 찾을 수 없습니다.", "권한");
            }

            if (userAccountOrNull.Position == null)
            {
                return decideUnpositionedAdministratorPermission(userAccountOrNull, permissionKey);
            }

            if (userAccountOrNull.Position.IsActive == false)
            {
                return PermissionDecision.Deny("비활성 직책입니다.", userAccountOrNull.Position.Name);
            }

            if (userAccountOrNull.Position.IsSystemAdministrator)
            {
                return PermissionDecision.Allow(userAccountOrNull.Position.Name);
            }

            List<PositionPermissionGrant> permissionGrants = userAccountOrNull.Position.PermissionGrants
                .Where(permissionGrant => permissionGrant.IsAllowed && permissionGrant.PermissionKey == requiredPermissionKey)
                .ToList();
            foreach (PositionPermissionGrant permissionGrant in permissionGrants)
            {
                bool isAllowed = isGrantAllowed(permissionGrant, userAccountOrNull, targetTeamIdOrNull, ownerUserIdOrNull);
                if (isAllowed)
                {
                    return PermissionDecision.Allow(getScopeLabel(permissionGrant, userAccountOrNull));
                }
            }

            return PermissionDecision.Deny("직책에 허용된 권한이 없습니다.", userAccountOrNull.Position.Name);
        }
    }

    private static async Task<UserAccount?> loadActiveUserAccountOrNullAsync(
        OrganizationHubDbContext dbContext,
        UserSession userSession,
        CancellationToken cancellationToken
    )
    {
        UserAccount? userAccountOrNull = await dbContext.UserAccounts
            .Include(userAccount => userAccount.Position)
            .FirstOrDefaultAsync(userAccount => userAccount.Id == userSession.UserId && userAccount.IsActive, cancellationToken);
        if (userAccountOrNull == null || userAccountOrNull.PositionId.HasValue == false)
        {
            return userAccountOrNull;
        }

        await dbContext.PositionPermissionGrants
            .Where(permissionGrant => permissionGrant.PositionId == userAccountOrNull.PositionId.Value)
            .LoadAsync(cancellationToken);

        return userAccountOrNull;
    }

    private PermissionDecision decideUnpositionedAdministratorPermission(UserAccount userAccount, PermissionKey permissionKey)
    {
        if (userAccount.Role == EUserRole.Admin)
        {
            return PermissionDecision.Allow("시스템 관리자");
        }

        return PermissionDecision.Deny("사용자에게 직책이 배정되지 않았습니다.", permissionKey.GetRequiredValue());
    }

    private bool isGrantAllowed(
        PositionPermissionGrant permissionGrant,
        UserAccount userAccount,
        OrganizationTeamId? targetTeamIdOrNull,
        UserAccountId? ownerUserIdOrNull
    )
    {
        switch (permissionGrant.ScopeType)
        {
            case EPermissionScopeType.Global:
                return true;
            case EPermissionScopeType.OwnTeam:
                return isOwnTeamTarget(userAccount, targetTeamIdOrNull);
            case EPermissionScopeType.OwnTeamAndChildren:
                return isOwnTeamTarget(userAccount, targetTeamIdOrNull);
            case EPermissionScopeType.SelectedTeam:
                return isSelectedTeamTarget(permissionGrant, targetTeamIdOrNull);
            case EPermissionScopeType.OwnCreated:
                return UserAccountId.GetValueOrNull(ownerUserIdOrNull) == userAccount.Id;
            default:
                return false;
        }
    }

    private bool isOwnTeamTarget(UserAccount userAccount, OrganizationTeamId? targetTeamIdOrNull)
    {
        int? targetTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(targetTeamIdOrNull);
        if (userAccount.TeamId.HasValue == false || targetTeamIdValueOrNull.HasValue == false)
        {
            return false;
        }

        return targetTeamIdValueOrNull == userAccount.TeamId;
    }

    private bool isSelectedTeamTarget(PositionPermissionGrant permissionGrant, OrganizationTeamId? targetTeamIdOrNull)
    {
        int? targetTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(targetTeamIdOrNull);
        if (permissionGrant.TargetTeamId.HasValue == false || targetTeamIdValueOrNull.HasValue == false)
        {
            return false;
        }

        return permissionGrant.TargetTeamId == targetTeamIdValueOrNull;
    }

    private string getScopeLabel(PositionPermissionGrant permissionGrant, UserAccount userAccount)
    {
        if (userAccount.Position != null)
        {
            return userAccount.Position.Name;
        }

        return permissionGrant.PermissionKey;
    }
}
