using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private ServiceResult validateSubmitCommand(ApprovalRequestCommand command, ApprovalCreateContext context)
    {
        if (command.StepCount.HasSupportedValue == false)
        {
            return ServiceResult.Fail("결재 단계를 선택해 주세요.");
        }

        ApprovalStepCount stepCount = command.StepCount.GetRequiredStepCount();
        if (context.AvailableStepCounts.Contains(stepCount) == false)
        {
            return ServiceResult.Fail("결재 단계를 선택해 주세요.");
        }

        if (command.DestinationFolderId.HasValue == false)
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        if (command.FileId.HasValue == false)
        {
            return ServiceResult.Fail("결재할 파일을 선택해 주세요.");
        }

        ApprovalUserId firstApproverUserId = command.FirstApproverUserId;
        ApprovalUserId secondApproverUserId = command.SecondApproverUserId;
        ApprovalUserId requestedFinalApproverUserId = command.FinalApproverUserId;
        ApprovalUserId finalApproverUserId = resolveFinalApproverUserId(context.FinalApproverOptions, requestedFinalApproverUserId);
        string firstApproverLabel = ApprovalStepLabelUtility.GetStepRoleName(stepCount, new ApprovalStepNumber(1));
        string secondApproverLabel = command.StepCount.RequiresSecondApprover ? ApprovalStepLabelUtility.GetStepRoleName(stepCount, new ApprovalStepNumber(2)) : string.Empty;
        string finalApproverLabel = ApprovalStepLabelUtility.GetStepRoleName(stepCount, stepCount.GetFinalStepNumber());

        if (command.StepCount.RequiresFirstApprover && firstApproverUserId.HasValue == false)
        {
            return ServiceResult.Fail(firstApproverLabel + "를 선택해 주세요.");
        }

        if (command.StepCount.RequiresFirstApprover && hasUserOption(context.ApproverOptions, firstApproverUserId) == false)
        {
            return ServiceResult.Fail(firstApproverLabel + "를 찾을 수 없습니다.");
        }

        if (command.StepCount.RequiresSecondApprover && secondApproverUserId.HasValue == false)
        {
            return ServiceResult.Fail(secondApproverLabel + "를 선택해 주세요.");
        }

        if (command.StepCount.RequiresSecondApprover && hasUserOption(context.ApproverOptions, secondApproverUserId) == false)
        {
            return ServiceResult.Fail(secondApproverLabel + "를 찾을 수 없습니다.");
        }

        if (finalApproverUserId.HasValue == false)
        {
            return ServiceResult.Fail(finalApproverLabel + "를 선택해 주세요.");
        }

        if (hasUserOption(context.FinalApproverOptions, finalApproverUserId) == false)
        {
            return ServiceResult.Fail(finalApproverLabel + "를 찾을 수 없습니다.");
        }

        HashSet<ApprovalUserId> approverUserIds = new HashSet<ApprovalUserId>();
        if (command.StepCount.RequiresFirstApprover && approverUserIds.Add(firstApproverUserId) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        if (command.StepCount.RequiresSecondApprover && approverUserIds.Add(secondApproverUserId) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        if (approverUserIds.Add(finalApproverUserId) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        return ServiceResult.Succeed();
    }

    private ApprovalUserId resolveFinalApproverUserId(
        IReadOnlyList<ApprovalUserOption> finalApproverOptions,
        ApprovalUserId requestedFinalApproverUserId
    )
    {
        if (finalApproverOptions.Count == 1)
        {
            return ApprovalUserId.CreateExisting(finalApproverOptions[0].UserId);
        }

        return requestedFinalApproverUserId;
    }

    private ApprovalUserOption getRequiredUserOption(IReadOnlyList<ApprovalUserOption> options, ApprovalUserId userId)
    {
        ApprovalUserOption? optionOrNull = options.FirstOrDefault(option => option.UserId == userId.GetRequiredValue());
        if (optionOrNull == null)
        {
            throw new InvalidOperationException("Approval user option was not found.");
        }

        return optionOrNull;
    }

    private bool hasUserOption(IReadOnlyList<ApprovalUserOption> options, ApprovalUserId userId)
    {
        if (userId.HasValue == false)
        {
            return false;
        }

        return options.Any(option => option.UserId == userId.GetRequiredValue());
    }

    private ApprovalUserOption? getFirstApproverOrNull(IReadOnlyList<ApprovalUserOption> options, ApprovalRequestCommand command)
    {
        if (command.StepCount.RequiresFirstApprover == false)
        {
            return null;
        }

        return getRequiredUserOption(options, command.FirstApproverUserId);
    }

    private ApprovalUserOption? getSecondApproverOrNull(IReadOnlyList<ApprovalUserOption> options, ApprovalRequestCommand command)
    {
        if (command.StepCount.RequiresSecondApprover == false)
        {
            return null;
        }

        return getRequiredUserOption(options, command.SecondApproverUserId);
    }

    private async Task<bool> isFileInsideFolderAsync(
        DriveFileMetadata fileMetadata,
        DriveFolderId sourceFolderId,
        CancellationToken cancellationToken
    )
    {
        if (fileMetadata.ParentFolderIds.Contains(sourceFolderId.GetRequiredValue()))
        {
            return true;
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(DriveItemId.CreateOptional(fileMetadata.Id), cancellationToken);
        return ancestorFolderIds.Contains(sourceFolderId);
    }

}
