using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private async Task<ServiceResult> preparePublicationAsync(OrganizationHubDbContext dbContext, ApprovalRequest request, EDriveFileNameConflictResolution conflictResolution, CancellationToken cancellationToken)
    {
        try
        {
            DriveFolderId folderId = DriveFolderId.CreateRequired(request.ApprovedFolderId);
            DriveItemName fileName = DriveItemName.CreateRequired(request.FileName);
            DriveFileNameConflictCheckResult conflict = await mDriveService.CheckFileNameConflictAsync(folderId, fileName, cancellationToken);
            bool isReplacement = conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile && conflict.HasConflict;
            DriveItemId targetFileId = isReplacement ? conflict.GetSingleExistingFileId() : await mDriveService.GenerateFileIdAsync(cancellationToken);
            if (isReplacement == false && conflict.HasConflict)
            {
                fileName = conflict.SuggestedNewFileName;
            }

            ApprovalPublication publication = new ApprovalPublication();
            publication.ApprovalRequestId = request.Id;
            publication.TargetFileId = targetFileId.GetRequiredValue();
            publication.FileName = fileName.GetRequiredValue();
            publication.OperationKey = Guid.NewGuid().ToString("N");
            publication.IsReplacement = isReplacement;
            publication.NextAttemptAt = DateTime.UtcNow;
            dbContext.ApprovalPublications.Add(publication);
            return ServiceResult.Succeed();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Approval publication preparation failed.");
            return ServiceResult.Fail("승인본 저장 작업을 준비하지 못했습니다. 잠시 후 다시 시도해 주세요.");
        }
    }

}
