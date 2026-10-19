using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed partial class ApprovalWorkflowManagementService
{
    private async Task<ServiceResult> validateWorkflowFormAsync(ApprovalWorkflowFormModel formModel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            return ServiceResult.Fail("결재 경로 이름을 입력해 주세요.");
        }

        if (ApprovalStepCount.IsSupportedValue(formModel.Steps.Count) == false)
        {
            return ServiceResult.Fail("결재 단계는 1개부터 " + ApprovalStepCount.MAXIMUM_VALUE + "개까지 설정할 수 있습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IReadOnlySet<int> teamIds = await dbContext.Teams.Select(team => team.Id).ToHashSetAsync(cancellationToken);
            IReadOnlySet<int> userIds = await dbContext.UserAccounts.Select(user => user.Id).ToHashSetAsync(cancellationToken);
            IReadOnlySet<int> positionIds = await dbContext.Positions.Select(position => position.Id).ToHashSetAsync(cancellationToken);
            Dictionary<int, int> positionTeamIdsByPositionId = await dbContext.Positions.ToDictionaryAsync(position => position.Id, position => position.TeamId, cancellationToken);
            IReadOnlySet<string> permissionKeys = PermissionCatalog.GetDefinitions().Select(definition => definition.Key).ToHashSet();

            int? appliesToTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(formModel.AppliesToTeamIdOrNull);
            if (appliesToTeamIdValueOrNull.HasValue && teamIds.Contains(appliesToTeamIdValueOrNull.GetValueOrDefault()) == false)
            {
                return ServiceResult.Fail("적용 팀을 찾을 수 없습니다.");
            }

            ServiceResult requesterPositionValidationResult = validateRequesterPositionCondition(formModel, appliesToTeamIdValueOrNull, positionTeamIdsByPositionId);
            if (requesterPositionValidationResult.IsSuccess == false)
            {
                return requesterPositionValidationResult;
            }

            foreach (ApprovalWorkflowStepFormModel stepFormModel in formModel.Steps.OrderBy(step => step.StepOrder))
            {
                ServiceResult stepValidationResult = validateWorkflowStep(stepFormModel, teamIds, userIds, positionIds, permissionKeys);
                if (stepValidationResult.IsSuccess == false)
                {
                    return stepValidationResult;
                }
            }
        }

        return ServiceResult.Succeed();
    }

    private ServiceResult validateRequesterPositionCondition(
        ApprovalWorkflowFormModel formModel,
        int? appliesToTeamIdValueOrNull,
        IReadOnlyDictionary<int, int> positionTeamIdsByPositionId)
    {
        int? appliesToPositionIdValueOrNull = OrganizationPositionId.GetValueOrNull(formModel.AppliesToPositionIdOrNull);
        if (appliesToPositionIdValueOrNull.HasValue == false)
        {
            return ServiceResult.Succeed();
        }

        int appliesToPositionIdValue = appliesToPositionIdValueOrNull.GetValueOrDefault();
        int positionTeamId;
        bool hasPosition = positionTeamIdsByPositionId.TryGetValue(appliesToPositionIdValue, out positionTeamId);
        if (hasPosition == false)
        {
            return ServiceResult.Fail("요청 직책을 찾을 수 없습니다.");
        }

        if (appliesToTeamIdValueOrNull.HasValue == false)
        {
            return ServiceResult.Succeed();
        }

        if (positionTeamId == appliesToTeamIdValueOrNull.GetValueOrDefault())
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("요청 직책은 선택한 요청 팀에 속해야 합니다.");
    }

    private ServiceResult validateWorkflowStep(
        ApprovalWorkflowStepFormModel stepFormModel,
        IReadOnlySet<int> teamIds,
        IReadOnlySet<int> userIds,
        IReadOnlySet<int> positionIds,
        IReadOnlySet<string> permissionKeys)
    {
        if (PermissionScopeSupport.IsSupported(stepFormModel.ScopeType) == false)
        {
            return ServiceResult.Fail("지원하지 않는 권한 범위입니다. 소속 팀 또는 지정한 팀을 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(stepFormModel.StepName))
        {
            return ServiceResult.Fail("결재 단계 이름을 입력해 주세요.");
        }

        switch (stepFormModel.ApproverSelectionType)
        {
            case EApproverSelectionType.SpecificUser:
                return validateRequiredUser(stepFormModel, userIds);
            case EApproverSelectionType.SpecificPosition:
            case EApproverSelectionType.PositionInRequesterTeam:
                return validateRequiredPosition(stepFormModel, positionIds);
            case EApproverSelectionType.PositionInSelectedTeam:
                return validateRequiredPositionAndTeam(stepFormModel, positionIds, teamIds);
            case EApproverSelectionType.PermissionHolder:
                return validateRequiredPermission(stepFormModel, permissionKeys);
            default:
                return ServiceResult.Fail("알 수 없는 승인자 선택 방식입니다.");
        }
    }

    private ServiceResult validateRequiredUser(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<int> userIds)
    {
        int? approverUserIdValueOrNull = ApprovalUserId.GetValueOrNull(stepFormModel.ApproverUserIdOrNull);
        if (approverUserIdValueOrNull.HasValue && userIds.Contains(approverUserIdValueOrNull.GetValueOrDefault()))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("특정 사용자 승인 단계에는 승인자를 선택해 주세요.");
    }

    private ServiceResult validateRequiredPosition(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<int> positionIds)
    {
        int? approverPositionIdValueOrNull = OrganizationPositionId.GetValueOrNull(stepFormModel.ApproverPositionIdOrNull);
        if (approverPositionIdValueOrNull.HasValue && positionIds.Contains(approverPositionIdValueOrNull.GetValueOrDefault()))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("직책 기준 승인 단계에는 직책을 선택해 주세요.");
    }

    private ServiceResult validateRequiredPositionAndTeam(
        ApprovalWorkflowStepFormModel stepFormModel,
        IReadOnlySet<int> positionIds,
        IReadOnlySet<int> teamIds
    )
    {
        ServiceResult positionValidationResult = validateRequiredPosition(stepFormModel, positionIds);
        if (positionValidationResult.IsSuccess == false)
        {
            return positionValidationResult;
        }

        int? approverTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(stepFormModel.ApproverTeamIdOrNull);
        if (approverTeamIdValueOrNull.HasValue && teamIds.Contains(approverTeamIdValueOrNull.GetValueOrDefault()))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("특정 팀 직책 승인 단계에는 팀을 선택해 주세요.");
    }

    private ServiceResult validateRequiredPermission(ApprovalWorkflowStepFormModel stepFormModel, IReadOnlySet<string> permissionKeys)
    {
        if (string.IsNullOrWhiteSpace(stepFormModel.RequiredPermissionKey) == false && permissionKeys.Contains(stepFormModel.RequiredPermissionKey))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("권한 보유자 승인 단계에는 권한을 선택해 주세요.");
    }
}
