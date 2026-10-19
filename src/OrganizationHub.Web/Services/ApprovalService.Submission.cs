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
    public async Task<ServiceResult> SubmitRequestAsync(UserSession userSession, ApprovalRequestCommand command, CancellationToken cancellationToken)
    {
        ApprovalCreateContext context = await GetCreateContextAsync(userSession, cancellationToken);
        if (context.CanCreateRequest == false)
        {
            return ServiceResult.Fail(context.BlockReason);
        }

        ServiceResult validationResult = validateSubmitCommand(command, context);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        DriveFolderId sourceFolderId = command.SourceFolderId;
        DriveFolderId destinationFolderId = command.DestinationFolderId;
        DriveItemId fileId = command.FileId;
        DriveFolderReference? sourceFolderOrNull = context.SourceFolders.FirstOrDefault(folder => folder.FolderId == sourceFolderId);
        if (sourceFolderOrNull == null)
        {
            return ServiceResult.Fail("결재 요청을 만들 수 없습니다.");
        }

        ServiceResult destinationValidationResult = await validateDestinationFolderAsync(userSession, command, context, cancellationToken);
        if (destinationValidationResult.IsSuccess == false)
        {
            return destinationValidationResult;
        }

        DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(fileId, cancellationToken);
        if (fileMetadataOrNull == null || fileMetadataOrNull.MimeType == GoogleMimeTypes.DRIVE_FOLDER)
        {
            return ServiceResult.Fail("결재할 파일을 선택해 주세요.");
        }

        DriveFileMetadata? destinationFolderMetadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(destinationFolderId.GetRequiredValue()), cancellationToken);
        if (destinationFolderMetadataOrNull == null || destinationFolderMetadataOrNull.MimeType != GoogleMimeTypes.DRIVE_FOLDER)
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        bool isFileInsideSourceFolder = await isFileInsideFolderAsync(fileMetadataOrNull, sourceFolderOrNull.FolderId, cancellationToken);
        if (isFileInsideSourceFolder == false)
        {
            return ServiceResult.Fail("선택한 파일을 결재 요청 위치에서 찾을 수 없습니다.");
        }

        ServiceResultOfT<ApprovalDocument> documentResult = await captureDocumentAsync(fileId, cancellationToken);
        if (documentResult.IsSuccess == false || documentResult.ValueOrNull == null)
        {
            return ServiceResult.Fail(documentResult.ErrorMessage);
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UserAccount? requesterUserOrNull = await loadUserAccountWithAffiliationOrNullAsync(
                dbContext,
                userSession.UserAccountId,
                cancellationToken);
            ValidatedApprovalSubmission submission = new ValidatedApprovalSubmission
            {
                UserSession = userSession,
                RequesterUserOrNull = requesterUserOrNull,
                Command = command,
                CreateContext = context,
                SourceFolder = sourceFolderOrNull,
                FileMetadata = fileMetadataOrNull,
                DestinationFolderMetadata = destinationFolderMetadataOrNull
            };
            ApprovalRequest request = createPendingApprovalRequest(submission);
            ApprovalDocument document = documentResult.ValueOrNull;
            request.FileName = document.FileName;
            request.MimeType = document.ContentType;
            request.DocumentHash = calculateDocumentHash(document.Content);
            document.ApprovalRequest = request;
            dbContext.ApprovalRequests.Add(request);
            dbContext.ApprovalDocuments.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);
            bumpApprovalVersion();
            return ServiceResult.Succeed();
        }
    }

    private async Task<ServiceResult> validateDestinationFolderAsync(
        UserSession userSession,
        ApprovalRequestCommand command,
        ApprovalCreateContext context,
        CancellationToken cancellationToken)
    {
        DriveFolderId destinationFolderId = command.DestinationFolderId;
        DriveFolderReference? destinationRootFolderOrNull = context.DestinationFolders.FirstOrDefault(folder => folder.FolderId == destinationFolderId);
        if (destinationRootFolderOrNull != null)
        {
            return ServiceResult.Succeed();
        }

        bool canStoreApprovedFile = await mPermissionService.CanStoreApprovedFileToFolderAsync(userSession, destinationFolderId, cancellationToken);
        if (canStoreApprovedFile == false)
        {
            return ServiceResult.Fail("승인 완료 후 저장할 위치를 선택해 주세요.");
        }

        return ServiceResult.Succeed();
    }

    private ApprovalRequest createPendingApprovalRequest(ValidatedApprovalSubmission submission)
    {
        ApprovalUserOption? firstApproverOrNull = getFirstApproverOrNull(submission.CreateContext.ApproverOptions, submission.Command);
        ApprovalUserOption? secondApproverOrNull = getSecondApproverOrNull(submission.CreateContext.ApproverOptions, submission.Command);
        ApprovalUserId requestedFinalApproverUserId = submission.Command.FinalApproverUserId;
        ApprovalUserId finalApproverUserId = resolveFinalApproverUserId(submission.CreateContext.FinalApproverOptions, requestedFinalApproverUserId);
        ApprovalUserOption finalApprover = getRequiredUserOption(submission.CreateContext.FinalApproverOptions, finalApproverUserId);
        ApprovalStepCount stepCount = submission.Command.StepCount.GetRequiredStepCount();
        ApprovalRequest request = createApprovalRequest(submission);
        if (submission.Command.StepCount.RequiresFirstApprover() && firstApproverOrNull != null)
        {
            request.Steps.Add(createApprovalStep(ApprovalStepNumber.CreateFirstApproverStep(), firstApproverOrNull));
        }

        if (submission.Command.StepCount.RequiresSecondApprover() && secondApproverOrNull != null)
        {
            request.Steps.Add(createApprovalStep(ApprovalStepNumber.CreateSecondApproverStep(), secondApproverOrNull));
        }

        request.Steps.Add(createApprovalStep(stepCount.GetFinalStepNumber(), finalApprover));
        request.AuditLogs.Add(createAuditLog(createAuditLogRequest(
            submission.UserSession,
            submission.RequesterUserOrNull,
            EApprovalAuditAction.Submitted,
            null,
            ApprovalComment.CreateOptional("결재 요청"))));
        return request;
    }
}
