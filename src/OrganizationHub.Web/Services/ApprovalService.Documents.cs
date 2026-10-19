using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    private const int SNAPSHOT_BUFFER_BYTES = 81_920;

    public async Task<FileDownloadResult?> GetDocumentSnapshotOrNullAsync(UserSession userSession, ApprovalRequestId requestId, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ApprovalRequest? requestOrNull = await loadApprovalRequestOrNullAsync(dbContext, requestId, cancellationToken);
            int currentUserId = userSession.UserAccountId.GetRequiredValue();
            bool isActiveUser = await dbContext.UserAccounts.AnyAsync(user => user.Id == currentUserId && user.IsActive && user.Position != null && user.Position.IsActive, cancellationToken);
            if (requestOrNull == null || isActiveUser == false)
            {
                return null;
            }

            bool isRelatedUser = requestOrNull.RequesterUserId == currentUserId || requestOrNull.Steps.Any(step =>
                step.ApproverUserId == currentUserId &&
                (step.Status != EApprovalStepStatus.Pending || (requestOrNull.Status == EApprovalRequestStatus.Pending && step.StepNumber <= requestOrNull.CurrentStepNumber)));
            if (isRelatedUser == false)
            {
                OrganizationTeamId? teamIdOrNull = OrganizationTeamId.CreateExistingOrNull(requestOrNull.TeamId);
                bool canReadHistory = await mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(PermissionKeys.APPROVAL_HISTORY_READ), teamIdOrNull, cancellationToken);
                if (canReadHistory == false)
                {
                    return null;
                }
            }

            ApprovalDocument? documentOrNull = await dbContext.ApprovalDocuments.AsNoTracking().SingleOrDefaultAsync(document => document.ApprovalRequestId == requestOrNull.Id, cancellationToken);
            if (documentOrNull == null || calculateDocumentHash(documentOrNull.Content) != requestOrNull.DocumentHash)
            {
                return null;
            }

            return new FileDownloadResult(new MemoryStream(documentOrNull.Content, false), DriveItemName.CreateRequired(documentOrNull.FileName), FileContentType.CreateOptional(documentOrNull.ContentType));
        }
    }

    private async Task<ServiceResultOfT<ApprovalDocument>> captureDocumentAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        try
        {
            FileDownloadResult downloadedFile = await mDriveService.DownloadFileSnapshotAsync(fileId, new FileByteLimit(mMaximumSnapshotBytes), cancellationToken);
            using (Stream sourceStream = downloadedFile.ContentStream)
            using (MemoryStream snapshotStream = new MemoryStream())
            {
                byte[] buffer = new byte[SNAPSHOT_BUFFER_BYTES];
                int bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken);
                while (bytesRead > 0)
                {
                    if (snapshotStream.Length + bytesRead > mMaximumSnapshotBytes)
                    {
                        return ServiceResultOfT<ApprovalDocument>.Fail("결재 문서가 허용된 파일 크기를 초과합니다.");
                    }

                    await snapshotStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken);
                }

                ApprovalDocument document = new ApprovalDocument();
                document.Content = snapshotStream.ToArray();
                document.FileName = downloadedFile.FileName;
                document.ContentType = downloadedFile.ContentType;
                return ServiceResultOfT<ApprovalDocument>.Succeed(document);
            }
        }
        catch (FileSizeLimitExceededException)
        {
            return ServiceResultOfT<ApprovalDocument>.Fail("결재 문서가 허용된 파일 크기를 초과합니다.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            mLogger.LogWarning(exception, "Approval document capture failed.");
            return ServiceResultOfT<ApprovalDocument>.Fail("결재 문서를 보존하지 못했습니다. 잠시 후 다시 요청해 주세요.");
        }
    }

    private static string calculateDocumentHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content));
    }
}
