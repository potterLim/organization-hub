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

public sealed partial class ApprovalPublicationService
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
                    .SetProperty(publication => publication.AttemptCount, publication => publication.AttemptCount + 1)
                    .SetProperty(publication => publication.LastAttemptAt, now)
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
                    ApprovalRequest? requestOrNull = await dbContext.ApprovalRequests.Include(item => item.RequesterUser).ThenInclude(user => user!.Team).Include(item => item.RequesterUser).ThenInclude(user => user!.Position).SingleOrDefaultAsync(item => item.Id == requiredRequestId, publicationToken);
                    ApprovalDocument document = await dbContext.ApprovalDocuments.SingleAsync(item => item.ApprovalRequestId == requiredRequestId, publicationToken);
                    if (requestOrNull == null || requestOrNull.Status != EApprovalRequestStatus.Publishing || Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(document.Content)) != requestOrNull.DocumentHash)
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
                    publication.LastError = string.Empty;
                    publication.LeaseExpiresAt = null;
                    dbContext.UploadAuditLogs.Add(createUploadAuditLogForApprovedCopy(requestOrNull, file));
                    await dbContext.SaveChangesAsync(publicationToken);
                    if (mDriveBrowserServiceOrNull != null)
                    {
                        mDriveBrowserServiceOrNull.InvalidateRecentFiles(upload.TargetFolderId);
                    }

                    mChangeVersionServiceOrNull?.Bump(ChangeVersionKeys.APPROVAL);
                    return ServiceResult.Succeed();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await releasePublicationLeaseAsync(requiredRequestId, leaseToken, "저장 작업이 중단되었습니다. 자동 재시도 예정입니다.");
            throw;
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Approval publication {RequestId} will be retried.", requiredRequestId);
            await releasePublicationLeaseAsync(requiredRequestId, leaseToken, getPublicationFailureDescription(exception));
            return ServiceResult.Fail("결재는 접수되었으나 승인본 저장이 완료되지 않았습니다. 자동으로 다시 시도하며, 내 결재함에서도 저장을 다시 시도할 수 있습니다.");
        }
    }

    private static string getPublicationFailureDescription(Exception exception)
    {
        if (exception is Google.GoogleApiException googleException)
        {
            if (googleException.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return "Google 계정의 승인본 폴더 쓰기 권한과 저장 용량을 확인해 주세요.";
            }

            if (googleException.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "승인본 폴더 또는 대상 파일을 찾을 수 없습니다. Drive 연결값을 확인해 주세요.";
            }
        }

        if (exception is OperationCanceledException)
        {
            return "Google 저장 응답 시간이 초과되었습니다. 연결 상태를 확인해 주세요.";
        }

        return "승인본 저장 또는 저장 결과 확인에 실패했습니다. 연결 상태와 서버 로그를 확인해 주세요.";
    }

    private async Task releasePublicationLeaseAsync(int requestId, string leaseToken, string errorDescription)
    {
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(CancellationToken.None))
            {
                await dbContext.ApprovalPublications.Where(publication => publication.ApprovalRequestId == requestId && publication.LeaseToken == leaseToken && publication.IsCompleted == false)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(publication => publication.LeaseExpiresAt, (DateTime?)null)
                        .SetProperty(publication => publication.LastError, errorDescription)
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
