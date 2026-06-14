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
                    userAccount.ID == userSession.UserID &&
                    userAccount.IsActive &&
                    userAccount.Position != null &&
                    userAccount.Position.IsActive,
                    cancellationToken);
            return hasConfiguredPosition;
        }
    }

    public async Task<bool> HasPermissionAsync(UserSession userSession, PermissionKey permissionKey, int? targetTeamIDOrNull, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecidePermissionAsync(userSession, permissionKey, targetTeamIDOrNull, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecidePermissionAsync(UserSession userSession, PermissionKey permissionKey, int? targetTeamIDOrNull, CancellationToken cancellationToken)
    {
        return await DecidePermissionAsync(userSession, permissionKey, targetTeamIDOrNull, null, cancellationToken);
    }

    public async Task<PermissionDecision> DecidePermissionAsync(UserSession userSession, PermissionKey permissionKey, int? targetTeamIDOrNull, int? ownerUserIDOrNull, CancellationToken cancellationToken)
    {
        if (PermissionCatalog.GetDefinitions().Any(definition => definition.Key == permissionKey.Value) == false)
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
                .Where(permissionGrant => permissionGrant.IsAllowed && permissionGrant.PermissionKey == permissionKey.Value)
                .ToList();
            foreach (PositionPermissionGrant permissionGrant in permissionGrants)
            {
                bool isAllowed = isGrantAllowed(permissionGrant, userAccountOrNull, targetTeamIDOrNull, ownerUserIDOrNull);
                if (isAllowed)
                {
                    return PermissionDecision.Allow(getScopeLabel(permissionGrant, userAccountOrNull));
                }
            }

            return PermissionDecision.Deny("직책에 허용된 권한이 없습니다.", userAccountOrNull.Position.Name);
        }
    }

    private static async Task<UserAccount?> loadActiveUserAccountOrNullAsync(OrganizationHubDbContext dbContext, UserSession userSession, CancellationToken cancellationToken)
    {
        UserAccount? userAccountOrNull = await dbContext.UserAccounts
            .Include(userAccount => userAccount.Position)
            .FirstOrDefaultAsync(userAccount => userAccount.ID == userSession.UserID && userAccount.IsActive, cancellationToken);
        if (userAccountOrNull == null || userAccountOrNull.PositionID.HasValue == false)
        {
            return userAccountOrNull;
        }

        await dbContext.PositionPermissionGrants
            .Where(permissionGrant => permissionGrant.PositionID == userAccountOrNull.PositionID.Value)
            .LoadAsync(cancellationToken);

        return userAccountOrNull;
    }

    private PermissionDecision decideUnpositionedAdministratorPermission(UserAccount userAccount, PermissionKey permissionKey)
    {
        if (userAccount.Role == EUserRole.Admin)
        {
            return PermissionDecision.Allow("시스템 관리자");
        }

        return PermissionDecision.Deny("사용자에게 직책이 배정되지 않았습니다.", permissionKey.Value);
    }

    private bool isGrantAllowed(PositionPermissionGrant permissionGrant, UserAccount userAccount, int? targetTeamIDOrNull, int? ownerUserIDOrNull)
    {
        switch (permissionGrant.ScopeType)
        {
            case EPermissionScopeType.Global:
                return true;
            case EPermissionScopeType.OwnTeam:
                return isOwnTeamTarget(userAccount, targetTeamIDOrNull);
            case EPermissionScopeType.OwnTeamAndChildren:
                return isOwnTeamTarget(userAccount, targetTeamIDOrNull);
            case EPermissionScopeType.SelectedTeam:
                return isSelectedTeamTarget(permissionGrant, targetTeamIDOrNull);
            case EPermissionScopeType.OwnCreated:
                return ownerUserIDOrNull.HasValue && ownerUserIDOrNull.Value == userAccount.ID;
            default:
                return false;
        }
    }

    private bool isOwnTeamTarget(UserAccount userAccount, int? targetTeamIDOrNull)
    {
        if (userAccount.TeamID.HasValue == false)
        {
            return false;
        }

        return targetTeamIDOrNull.HasValue && targetTeamIDOrNull.Value == userAccount.TeamID.Value;
    }

    private bool isSelectedTeamTarget(PositionPermissionGrant permissionGrant, int? targetTeamIDOrNull)
    {
        if (permissionGrant.TargetTeamID.HasValue == false || targetTeamIDOrNull.HasValue == false)
        {
            return false;
        }

        return permissionGrant.TargetTeamID.Value == targetTeamIDOrNull.Value;
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
