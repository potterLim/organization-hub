using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private ServiceResult validateSubmitForm(ApprovalRequestFormModel formModel, ApprovalCreateContext context)
    {
        if (context.AvailableStepCounts.Contains(formModel.StepCount) == false)
        {
            return ServiceResult.Fail("결재 단계를 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.DestinationFolderID))
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.FileID))
        {
            return ServiceResult.Fail("결재할 파일을 선택해 주세요.");
        }

        if (formModel.StepCount >= 2 && formModel.FirstApproverUserID <= 0)
        {
            return ServiceResult.Fail("1차 결재권자를 선택해 주세요.");
        }

        if (formModel.StepCount >= 2 && context.ApproverOptions.Any(option => option.UserID == formModel.FirstApproverUserID) == false)
        {
            return ServiceResult.Fail("1차 결재권자를 찾을 수 없습니다.");
        }

        if (formModel.StepCount == 3 && formModel.SecondApproverUserID <= 0)
        {
            return ServiceResult.Fail("2차 결재권자를 선택해 주세요.");
        }

        if (formModel.StepCount == 3 && context.ApproverOptions.Any(option => option.UserID == formModel.SecondApproverUserID) == false)
        {
            return ServiceResult.Fail("2차 결재권자를 찾을 수 없습니다.");
        }

        int finalApproverUserID = resolveFinalApproverUserID(context.FinalApproverOptions, formModel.FinalApproverUserID);
        if (finalApproverUserID <= 0)
        {
            return ServiceResult.Fail("최종 결재권자를 선택해 주세요.");
        }

        if (context.FinalApproverOptions.Any(option => option.UserID == finalApproverUserID) == false)
        {
            return ServiceResult.Fail("최종 결재권자를 찾을 수 없습니다.");
        }

        HashSet<int> approverUserIDs = new HashSet<int>();
        if (formModel.StepCount >= 2 && approverUserIDs.Add(formModel.FirstApproverUserID) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        if (formModel.StepCount == 3 && approverUserIDs.Add(formModel.SecondApproverUserID) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        if (approverUserIDs.Add(finalApproverUserID) == false)
        {
            return ServiceResult.Fail("결재권자는 중복될 수 없습니다.");
        }

        return ServiceResult.Succeed();
    }

    private int resolveFinalApproverUserID(IReadOnlyList<ApprovalUserOption> finalApproverOptions, int requestedFinalApproverUserID)
    {
        if (finalApproverOptions.Count == 1)
        {
            return finalApproverOptions[0].UserID;
        }

        return requestedFinalApproverUserID;
    }

    private ApprovalUserOption getRequiredUserOption(IReadOnlyList<ApprovalUserOption> options, int userID)
    {
        ApprovalUserOption? optionOrNull = options.FirstOrDefault(option => option.UserID == userID);
        if (optionOrNull == null)
        {
            throw new InvalidOperationException("Approval user option was not found.");
        }

        return optionOrNull;
    }

    private ApprovalUserOption? getFirstApproverOrNull(IReadOnlyList<ApprovalUserOption> options, ApprovalRequestFormModel formModel)
    {
        if (formModel.StepCount < 2)
        {
            return null;
        }

        return getRequiredUserOption(options, formModel.FirstApproverUserID);
    }

    private ApprovalUserOption? getSecondApproverOrNull(IReadOnlyList<ApprovalUserOption> options, ApprovalRequestFormModel formModel)
    {
        if (formModel.StepCount != 3)
        {
            return null;
        }

        return getRequiredUserOption(options, formModel.SecondApproverUserID);
    }

    private async Task<bool> isFileInsideFolderAsync(DriveFileMetadata fileMetadata, string sourceFolderID, CancellationToken cancellationToken)
    {
        if (fileMetadata.ParentFolderIDs.Contains(sourceFolderID))
        {
            return true;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(fileMetadata.ID, cancellationToken);
        return ancestorFolderIDs.Contains(sourceFolderID);
    }
}
