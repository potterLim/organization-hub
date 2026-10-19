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

internal sealed class PositionManagementService : IPositionManagementService
{
    private const int SORT_ORDER_STEP = 10;

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
                .AsSplitQuery()
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
            OrganizationTeamId teamId = formModel.TeamIdValue;
            int requiredTeamId = teamId.GetRequiredValue();
            string trimmedName = formModel.Name.Trim();
            bool hasDuplicateName = await dbContext.Positions.AnyAsync(position => position.TeamId == requiredTeamId && position.Name == trimmedName, cancellationToken);
            if (hasDuplicateName)
            {
                return ServiceResult.Fail("같은 팀에 이미 같은 이름의 직책이 있습니다.");
            }

            OrganizationPosition position = new OrganizationPosition();
            applyPositionForm(position, formModel);
            dbContext.Positions.Add(position);
            addPermissionGrants(dbContext, position, formModel);
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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
            OrganizationPositionId positionId = formModel.PositionIdValue;
            OrganizationTeamId teamId = formModel.TeamIdValue;
            int requiredPositionId = positionId.GetRequiredValue();
            int requiredTeamId = teamId.GetRequiredValue();
            OrganizationPosition? positionOrNull = await dbContext.Positions
                .Include(position => position.PermissionGrants)
                .FirstOrDefaultAsync(position => position.Id == requiredPositionId, cancellationToken);
            if (positionOrNull == null)
            {
                return ServiceResult.Fail("직책을 찾을 수 없습니다.");
            }

            if (positionOrNull.Revision != formModel.Revision)
            {
                return AdministrativeSaveUtility.CreateConflictResult();
            }

            bool hasDuplicateName = await dbContext.Positions.AnyAsync(
                position => position.Id != requiredPositionId &&
                    position.TeamId == requiredTeamId &&
                    position.Name == formModel.Name.Trim(),
                cancellationToken);
            if (hasDuplicateName)
            {
                return ServiceResult.Fail("같은 팀에 이미 같은 이름의 직책이 있습니다.");
            }

            applyPositionForm(positionOrNull, formModel);
            dbContext.PositionPermissionGrants.RemoveRange(positionOrNull.PermissionGrants);
            addPermissionGrants(dbContext, positionOrNull, formModel);
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    public async Task<ServiceResult> UpdatePositionOrderAsync(OrganizationPositionOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedPositionIds.Count == 0)
        {
            return ServiceResult.Succeed();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredTeamId = request.TeamId.GetRequiredValue();
            if (OrderUpdateRequestValidation.hasDuplicateValues(request.OrderedPositionIds))
            {
                return ServiceResult.Fail("정렬할 직책이 중복되어 있습니다.");
            }

            List<int> orderedPositionIdValues = request.OrderedPositionIds.Select(positionId => positionId.GetRequiredValue()).ToList();
            List<OrganizationPosition> positions = await dbContext.Positions
                .Where(position => position.TeamId == requiredTeamId && orderedPositionIdValues.Contains(position.Id))
                .ToListAsync(cancellationToken);
            if (positions.Count != orderedPositionIdValues.Count)
            {
                return ServiceResult.Fail("정렬할 직책을 찾을 수 없습니다.");
            }

            foreach (OrganizationPosition item in positions)
            {
                int expectedRevision;
                if (request.ExpectedRevisions.TryGetValue(item.Id, out expectedRevision) == false || item.Revision != expectedRevision)
                {
                    return AdministrativeSaveUtility.CreateConflictResult();
                }
                dbContext.Entry(item).Property(value => value.Revision).IsModified = true;
            }

            Dictionary<int, OrganizationPosition> positionsById = positions.ToDictionary(position => position.Id);
            int sortOrder = SORT_ORDER_STEP;
            foreach (OrganizationPositionId positionId in request.OrderedPositionIds)
            {
                OrganizationPosition position = positionsById[positionId.GetRequiredValue()];
                position.SortOrder = sortOrder;
                sortOrder += SORT_ORDER_STEP;
            }

            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    public async Task<ServiceResult> ActivatePositionAsync(OrganizationPositionId positionId, CancellationToken cancellationToken)
    {
        return await setPositionActivationStateAsync(positionId, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivatePositionAsync(OrganizationPositionId positionId, CancellationToken cancellationToken)
    {
        return await setPositionActivationStateAsync(positionId, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setPositionActivationStateAsync(
        OrganizationPositionId positionId,
        EActivationState activationState,
        CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredPositionId = positionId.GetRequiredValue();
            OrganizationPosition? positionOrNull = await dbContext.Positions.FirstOrDefaultAsync(position => position.Id == requiredPositionId, cancellationToken);
            if (positionOrNull == null)
            {
                return ServiceResult.Fail("직책을 찾을 수 없습니다.");
            }

            if (positionOrNull.IsSystemAdministrator && activationState == EActivationState.Inactive)
            {
                return ServiceResult.Fail("시스템 관리자 직책은 비활성화할 수 없습니다.");
            }

            positionOrNull.IsActive = activationState == EActivationState.Active;
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    private async Task<ServiceResult> validatePositionFormAsync(PositionFormModel formModel, CancellationToken cancellationToken)
    {
        OrganizationTeamId teamId = formModel.TeamIdValue;
        if (teamId.HasValue == false)
        {
            return ServiceResult.Fail("팀을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            return ServiceResult.Fail("직책 이름을 입력해 주세요.");
        }

        int requiredTeamId = teamId.GetRequiredValue();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            bool hasTeam = await dbContext.Teams.AnyAsync(team => team.Id == requiredTeamId && team.IsActive, cancellationToken);
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

        PermissionKey permissionKey = grantFormModel.PermissionKeyValue;
        if (PermissionScopeSupport.IsSupported(grantFormModel.ScopeType) == false)
        {
            return ServiceResult.Fail("지원하지 않는 권한 범위입니다. 소속 팀 또는 지정한 팀을 선택해 주세요.");
        }

        string requiredPermissionKey = permissionKey.GetRequiredValue();
        if (permissionKeys.Contains(requiredPermissionKey) == false)
        {
            return ServiceResult.Fail("알 수 없는 권한이 포함되어 있습니다.");
        }

        if (grantFormModel.ScopeType == EPermissionScopeType.SelectedTeam && grantFormModel.TargetTeamIdOrNull.HasValue == false)
        {
            return ServiceResult.Fail("특정 팀 범위 권한에는 대상 팀이 필요합니다.");
        }

        return ServiceResult.Succeed();
    }

    private void applyPositionForm(OrganizationPosition position, PositionFormModel formModel)
    {
        OrganizationTeamId teamId = formModel.TeamIdValue;
        position.TeamId = teamId.GetRequiredValue();
        position.Name = formModel.Name.Trim();
        position.Description = getTrimmedTextOrEmpty(formModel.Description);
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
            permissionGrant.PermissionKey = grantFormModel.PermissionKeyValue.GetRequiredValue();
            permissionGrant.ScopeType = grantFormModel.ScopeType;
            permissionGrant.TargetTeamId = null;
            if (grantFormModel.ScopeType == EPermissionScopeType.SelectedTeam)
            {
                permissionGrant.TargetTeamId = OrganizationTeamId.GetValueOrNull(grantFormModel.TargetTeamIdOrNull);
            }

            permissionGrant.IsAllowed = true;
            dbContext.PositionPermissionGrants.Add(permissionGrant);
        }
    }

    private string getTrimmedTextOrEmpty(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Trim();
    }
}
