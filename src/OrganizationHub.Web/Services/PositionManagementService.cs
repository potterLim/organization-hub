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

public sealed class PositionManagementService : IPositionManagementService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    public PositionManagementService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<OrganizationPosition>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<OrganizationPosition> positions = await dbContext.Positions
                .Include(position => position.Team)
                .Include(position => position.UserAccounts)
                .Include(position => position.PermissionGrants)
                .ThenInclude(permissionGrant => permissionGrant.TargetTeam)
                .OrderBy(position => position.Team == null ? 0 : position.Team.SortOrder)
                .ThenBy(position => position.SortOrder)
                .ThenBy(position => position.Name)
                .ToListAsync(cancellationToken);
            return positions;
        }
    }

    public Task<IReadOnlyList<PermissionDefinition>> GetPermissionDefinitionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionDefinition> definitions = PermissionCatalog.GetDefinitions();
        return Task.FromResult(definitions);
    }

    public async Task<ServiceResult> CreatePositionAsync(PositionFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult validationResult = await validatePositionFormAsync(formModel, cancellationToken);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            bool hasDuplicateName = await dbContext.Positions.AnyAsync(position => position.TeamID == formModel.TeamID && position.Name == formModel.Name.Trim(), cancellationToken);
            if (hasDuplicateName)
            {
                return ServiceResult.Fail("같은 팀에 이미 같은 이름의 직책이 있습니다.");
            }

            OrganizationPosition position = new OrganizationPosition();
            applyPositionForm(position, formModel);
            dbContext.Positions.Add(position);
            addPermissionGrants(dbContext, position, formModel);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> UpdatePositionAsync(PositionFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult validationResult = await validatePositionFormAsync(formModel, cancellationToken);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            OrganizationPosition? positionOrNull = await dbContext.Positions.Include(position => position.PermissionGrants).FirstOrDefaultAsync(position => position.ID == formModel.ID, cancellationToken);
            if (positionOrNull == null)
            {
                return ServiceResult.Fail("직책을 찾을 수 없습니다.");
            }

            bool hasDuplicateName = await dbContext.Positions.AnyAsync(position => position.ID != formModel.ID && position.TeamID == formModel.TeamID && position.Name == formModel.Name.Trim(), cancellationToken);
            if (hasDuplicateName)
            {
                return ServiceResult.Fail("같은 팀에 이미 같은 이름의 직책이 있습니다.");
            }

            applyPositionForm(positionOrNull, formModel);
            dbContext.PositionPermissionGrants.RemoveRange(positionOrNull.PermissionGrants);
            addPermissionGrants(dbContext, positionOrNull, formModel);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> ActivatePositionAsync(int positionID, CancellationToken cancellationToken)
    {
        return await setPositionActivationStateAsync(positionID, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivatePositionAsync(int positionID, CancellationToken cancellationToken)
    {
        return await setPositionActivationStateAsync(positionID, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setPositionActivationStateAsync(int positionID, EActivationState activationState, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            OrganizationPosition? positionOrNull = await dbContext.Positions.FirstOrDefaultAsync(position => position.ID == positionID, cancellationToken);
            if (positionOrNull == null)
            {
                return ServiceResult.Fail("직책을 찾을 수 없습니다.");
            }

            if (positionOrNull.IsSystemAdministrator && activationState == EActivationState.Inactive)
            {
                return ServiceResult.Fail("시스템 관리자 직책은 비활성화할 수 없습니다.");
            }

            positionOrNull.IsActive = activationState == EActivationState.Active;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private async Task<ServiceResult> validatePositionFormAsync(PositionFormModel formModel, CancellationToken cancellationToken)
    {
        if (formModel.TeamID <= 0)
        {
            return ServiceResult.Fail("팀을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            return ServiceResult.Fail("직책 이름을 입력해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            bool hasTeam = await dbContext.Teams.AnyAsync(team => team.ID == formModel.TeamID && team.IsActive, cancellationToken);
            if (hasTeam == false)
            {
                return ServiceResult.Fail("활성 팀을 선택해 주세요.");
            }

            IReadOnlySet<string> permissionKeys = PermissionCatalog.GetDefinitions().Select(definition => definition.Key).ToHashSet();
            foreach (PositionPermissionGrantFormModel grantFormModel in formModel.PermissionGrants)
            {
                ServiceResult grantValidationResult = validatePermissionGrant(grantFormModel, permissionKeys);
                if (grantValidationResult.IsSuccess == false)
                {
                    return grantValidationResult;
                }
            }
        }

        return ServiceResult.Succeed();
    }

    private ServiceResult validatePermissionGrant(PositionPermissionGrantFormModel grantFormModel, IReadOnlySet<string> permissionKeys)
    {
        if (grantFormModel.IsAllowed == false)
        {
            return ServiceResult.Succeed();
        }

        if (permissionKeys.Contains(grantFormModel.PermissionKey) == false)
        {
            return ServiceResult.Fail("알 수 없는 권한이 포함되어 있습니다.");
        }

        if (grantFormModel.ScopeType == EPermissionScopeType.SelectedTeam && grantFormModel.TargetTeamID.HasValue == false)
        {
            return ServiceResult.Fail("특정 팀 범위 권한에는 대상 팀이 필요합니다.");
        }

        return ServiceResult.Succeed();
    }

    private void applyPositionForm(OrganizationPosition position, PositionFormModel formModel)
    {
        position.TeamID = formModel.TeamID;
        position.Name = formModel.Name.Trim();
        position.Description = getEmptyAsEmpty(formModel.Description);
        position.SortOrder = formModel.SortOrder;
        position.IsActive = formModel.IsActive;
        position.IsSystemAdministrator = formModel.IsSystemAdministrator;
    }

    private void addPermissionGrants(OrganizationHubDbContext dbContext, OrganizationPosition position, PositionFormModel formModel)
    {
        foreach (PositionPermissionGrantFormModel grantFormModel in formModel.PermissionGrants)
        {
            if (grantFormModel.IsAllowed == false)
            {
                continue;
            }

            PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
            permissionGrant.Position = position;
            permissionGrant.PermissionKey = grantFormModel.PermissionKey;
            permissionGrant.ScopeType = grantFormModel.ScopeType;
            permissionGrant.TargetTeamID = grantFormModel.ScopeType == EPermissionScopeType.SelectedTeam ? grantFormModel.TargetTeamID : null;
            permissionGrant.IsAllowed = true;
            dbContext.PositionPermissionGrants.Add(permissionGrant);
        }
    }

    private string getEmptyAsEmpty(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }
}
