using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private static readonly TimeSpan PUBLICATION_LEASE_DURATION = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PUBLICATION_RETRY_DELAY = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PUBLICATION_TIMEOUT = TimeSpan.FromSeconds(90);

    public async Task<ServiceResult> PublishApprovedFileAsync(ApprovalRequestId requestId, CancellationToken cancellationToken)
    {
        int requiredRequestId = requestId.GetRequiredValue();
        string leaseToken = Guid.NewGuid().ToString("N");
        DateTime now = DateTime.UtcNow;
        await using (OrganizationHubDbContext claimContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int claimedCount = await claimContext.ApprovalPublications
                .Where(publication => publication.ApprovalRequestId == requiredRequestId && publication.IsCompleted == false &&
                    (publication.LeaseExpiresAt == null || publication.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(setters => setters.SetProperty(publication => publication.LeaseToken, leaseToken)
                    .SetProperty(publication => publication.LeaseExpiresAt, now.Add(PUBLICATION_LEASE_DURATION)), cancellationToken);
            if (claimedCount == 0)
            {
                bool isCompleted = await claimContext.ApprovalPublications.AnyAsync(publication => publication.ApprovalRequestId == requiredRequestId && publication.IsCompleted, cancellationToken);
                return isCompleted ? ServiceResult.Succeed() : ServiceResult.Fail("승인본을 저장 중입니다. 잠시 후 목록을 확인해 주세요.");
            }
        }

        try
        {
            using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutSource.CancelAfter(PUBLICATION_TIMEOUT);
                CancellationToken publicationToken = timeoutSource.Token;
                await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(publicationToken))
                {
                    ApprovalPublication publication = await dbContext.ApprovalPublications.SingleAsync(item => item.ApprovalRequestId == requiredRequestId && item.LeaseToken == leaseToken, publicationToken);
                    ApprovalRequest? requestOrNull = await loadApprovalRequestOrNullAsync(dbContext, requestId, publicationToken);
                    ApprovalDocument document = await dbContext.ApprovalDocuments.SingleAsync(item => item.ApprovalRequestId == requiredRequestId, publicationToken);
                    if (requestOrNull == null || requestOrNull.Status != EApprovalRequestStatus.Publishing || calculateDocumentHash(document.Content) != requestOrNull.DocumentHash)
                    {
                        throw new InvalidOperationException("Approval publication state or document hash is invalid.");
                    }

                    ApprovalFilePublishRequest upload = new ApprovalFilePublishRequest
                    {
                        TargetFileId = DriveItemId.CreateRequired(publication.TargetFileId),
                        TargetFolderId = DriveFolderId.CreateRequired(requestOrNull.ApprovedFolderId),
                        FileName = DriveItemName.CreateRequired(publication.FileName),
                        ContentType = FileContentType.CreateOptional(document.ContentType),
                        Content = document.Content,
                        OperationKey = publication.OperationKey,
                        IsReplacement = publication.IsReplacement
                    };
                    DriveFileItem file = await mDriveService.PublishApprovalFileAsync(upload, publicationToken);
                    if (file.Id != publication.TargetFileId)
                    {
                        throw new InvalidOperationException("Approval publication returned an unexpected file ID.");
                    }

                    // The lease may have expired while the remote operation was in progress.
                    bool hasLease = await dbContext.ApprovalPublications.AsNoTracking().AnyAsync(item => item.ApprovalRequestId == requiredRequestId && item.LeaseToken == leaseToken, publicationToken);
                    if (hasLease == false)
                    {
                        return ServiceResult.Fail("승인본 저장 작업을 다시 확인 중입니다.");
                    }

                    requestOrNull.ApprovedFileId = file.Id;
                    requestOrNull.Status = EApprovalRequestStatus.Approved;
                    requestOrNull.CompletedAt = DateTime.UtcNow;
                    requestOrNull.Revision++;
                    publication.IsCompleted = true;
                    publication.LeaseExpiresAt = null;
                    dbContext.UploadAuditLogs.Add(createUploadAuditLogForApprovedCopy(requestOrNull, file));
                    await dbContext.SaveChangesAsync(publicationToken);
                    if (mDriveBrowserServiceOrNull != null)
                    {
                        mDriveBrowserServiceOrNull.InvalidateRecentFiles(upload.TargetFolderId);
                    }

                    bumpApprovalVersion();
                    return ServiceResult.Succeed();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await releasePublicationLeaseAsync(requiredRequestId, leaseToken);
            throw;
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Approval publication {RequestId} will be retried.", requiredRequestId);
            await releasePublicationLeaseAsync(requiredRequestId, leaseToken);
            return ServiceResult.Fail("결재는 접수되었으나 승인본 저장이 완료되지 않았습니다. 자동으로 다시 시도하며, 내 결재함에서도 저장을 다시 시도할 수 있습니다.");
        }
    }

    private async Task<ServiceResult> preparePublicationAsync(OrganizationHubDbContext dbContext, ApprovalRequest request, EDriveFileNameConflictResolution conflictResolution, CancellationToken cancellationToken)
    {
        try
        {
            DriveFolderId folderId = DriveFolderId.CreateRequired(request.ApprovedFolderId);
            DriveItemName fileName = DriveItemName.CreateRequired(request.FileName);
            DriveFileNameConflictCheckResult conflict = await mDriveService.CheckFileNameConflictAsync(folderId, fileName, cancellationToken);
            bool isReplacement = conflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile && conflict.HasConflict;
            DriveItemId targetFileId = isReplacement ? conflict.GetFirstExistingFileId() : await mDriveService.GenerateFileIdAsync(cancellationToken);
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

    private async Task releasePublicationLeaseAsync(int requestId, string leaseToken)
    {
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(CancellationToken.None))
            {
                await dbContext.ApprovalPublications.Where(publication => publication.ApprovalRequestId == requestId && publication.LeaseToken == leaseToken && publication.IsCompleted == false)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(publication => publication.LeaseExpiresAt, (DateTime?)null)
                        .SetProperty(publication => publication.NextAttemptAt, DateTime.UtcNow.Add(PUBLICATION_RETRY_DELAY)), CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Approval publication {RequestId} lease will expire for recovery.", requestId);
        }
    }

    private UploadAuditLog createUploadAuditLogForApprovedCopy(ApprovalRequest request, DriveFileItem copiedFile)
    {
        UploadAuditLog auditLog = new UploadAuditLog();
        auditLog.FileId = copiedFile.Id;
        auditLog.FileName = copiedFile.Name;
        auditLog.FolderId = request.ApprovedFolderId.Trim();
        auditLog.FolderDisplayName = request.ApprovedFolderDisplayName;
        auditLog.FolderKind = EDriveFolderKind.TeamApproved;
        auditLog.UploaderUserId = request.RequesterUserId;
        auditLog.UploaderDisplayName = request.RequesterDisplayName;
        auditLog.UploaderStoredCompatibilityRole = getRequesterRole(request);
        auditLog.UploaderTeamId = getRequesterTeamIdOrNull(request);
        auditLog.UploaderTeamName = getRequesterTeamName(request);
        auditLog.UploaderPositionId = getRequesterPositionIdOrNull(request);
        auditLog.UploaderPositionName = getRequesterPositionName(request);
        auditLog.TeamId = request.TeamId;
        auditLog.TeamName = request.TeamName;
        auditLog.UploadedAt = DateTime.UtcNow;
        return auditLog;
    }
}
