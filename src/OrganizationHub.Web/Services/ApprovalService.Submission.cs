using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    public async Task<ServiceResult> SubmitRequestAsync(UserSession userSession, ApprovalRequestFormModel formModel, CancellationToken cancellationToken)
    {
        ApprovalCreateContext context = await GetCreateContextAsync(userSession, cancellationToken);
        if (context.CanCreateRequest == false)
        {
            return ServiceResult.Fail(context.BlockReason);
        }

        ServiceResult validationResult = validateSubmitForm(formModel, context);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        DriveFolderReference? sourceFolderOrNull = context.SourceFolders.FirstOrDefault(folder => folder.FolderID == formModel.SourceFolderID);
        if (sourceFolderOrNull == null)
        {
            return ServiceResult.Fail("결재 요청을 만들 수 없습니다.");
        }

        ServiceResult destinationValidationResult = await validateDestinationFolderAsync(userSession, formModel, context, cancellationToken);
        if (destinationValidationResult.IsSuccess == false)
        {
            return destinationValidationResult;
        }

        DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(formModel.FileID, cancellationToken);
        if (fileMetadataOrNull == null || fileMetadataOrNull.MimeType == "application/vnd.google-apps.folder")
        {
            return ServiceResult.Fail("결재할 파일을 선택해 주세요.");
        }

        DriveFileMetadata? destinationFolderMetadataOrNull = await mDriveService.GetFileOrNullAsync(formModel.DestinationFolderID, cancellationToken);
        if (destinationFolderMetadataOrNull == null || destinationFolderMetadataOrNull.MimeType != "application/vnd.google-apps.folder")
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        bool isFileInsideSourceFolder = await isFileInsideFolderAsync(fileMetadataOrNull, sourceFolderOrNull.FolderID, cancellationToken);
        if (isFileInsideSourceFolder == false)
        {
            return ServiceResult.Fail("선택한 파일을 결재 요청 위치에서 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalRequest request = createPendingApprovalRequest(userSession, formModel, context, sourceFolderOrNull, fileMetadataOrNull, destinationFolderMetadataOrNull);
            dbContext.ApprovalRequests.Add(request);
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    private async Task<ServiceResult> validateDestinationFolderAsync(UserSession userSession, ApprovalRequestFormModel formModel, ApprovalCreateContext context, CancellationToken cancellationToken)
    {
        DriveFolderReference? destinationRootFolderOrNull = context.DestinationFolders.FirstOrDefault(folder => folder.FolderID == formModel.DestinationFolderID);
        if (destinationRootFolderOrNull != null)
        {
            return ServiceResult.Succeed();
        }

        bool canStoreApprovedFile = await mPermissionService.CanStoreApprovedFileToFolderAsync(userSession, formModel.DestinationFolderID, cancellationToken);
        if (canStoreApprovedFile == false)
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        return ServiceResult.Succeed();
    }

    private ApprovalRequest createPendingApprovalRequest(UserSession userSession, ApprovalRequestFormModel formModel, ApprovalCreateContext context, DriveFolderReference sourceFolder, DriveFileMetadata fileMetadata, DriveFileMetadata destinationFolderMetadata)
    {
        ApprovalUserOption? firstApproverOrNull = getFirstApproverOrNull(context.ApproverOptions, formModel);
        ApprovalUserOption? secondApproverOrNull = getSecondApproverOrNull(context.ApproverOptions, formModel);
        ApprovalUserOption finalApprover = getRequiredUserOption(context.FinalApproverOptions, resolveFinalApproverUserID(context.FinalApproverOptions, formModel.FinalApproverUserID));
        ApprovalRequest request = createApprovalRequest(userSession, formModel, sourceFolder, fileMetadata, destinationFolderMetadata);
        if (formModel.StepCount >= 2 && firstApproverOrNull != null)
        {
            request.Steps.Add(createApprovalStep(1, firstApproverOrNull));
        }

        if (formModel.StepCount == 3 && secondApproverOrNull != null)
        {
            request.Steps.Add(createApprovalStep(2, secondApproverOrNull));
        }

        request.Steps.Add(createApprovalStep(formModel.StepCount, finalApprover));
        request.AuditLogs.Add(createAuditLog(userSession, EApprovalAuditAction.Submitted, null, "결재 요청", string.Empty, string.Empty));
        return request;
    }
}
