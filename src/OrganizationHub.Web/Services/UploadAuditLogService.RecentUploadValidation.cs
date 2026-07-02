using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Google;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService
{
    private async Task<UploadAuditLogItem?> tryCreateRecentReadableRootItemOrNullAsync(
        RecentReadableRootUploadFilterContext filterContext,
        UploadAuditLog auditLog
    )
    {
        try
        {
            return await createRecentReadableRootItemOrNullAsync(filterContext, auditLog);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GoogleApiException exception)
        {
            return handleRecentUploadValidationGoogleApiExceptionOrNull(exception, auditLog, filterContext.UploaderUsersById);
        }
        catch (HttpRequestException exception)
        {
            return handleTransientRecentUploadValidationException(exception, auditLog, filterContext.UploaderUsersById);
        }
        catch (IOException exception)
        {
            return handleTransientRecentUploadValidationException(exception, auditLog, filterContext.UploaderUsersById);
        }
    }

    private async Task<UploadAuditLogItem?> createRecentReadableRootItemOrNullAsync(
        RecentReadableRootUploadFilterContext filterContext,
        UploadAuditLog auditLog
    )
    {
        DriveFolderReference? matchingRootOrNull = await getMatchingReadableRootOrNullAsync(auditLog, filterContext.ReadableRoots, filterContext.CancellationToken);
        if (matchingRootOrNull == null)
        {
            return null;
        }

        DriveFileMetadata? fileMetadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateOptional(auditLog.FileId), filterContext.CancellationToken);
        if (fileMetadataOrNull == null || fileMetadataOrNull.MimeType == GoogleMimeTypes.DRIVE_FOLDER)
        {
            return null;
        }

        bool isFileStillInsideRoot = await isUploadInsideRootAsync(DriveItemId.CreateOptional(auditLog.FileId), matchingRootOrNull.FolderId, filterContext.CancellationToken);
        if (isFileStillInsideRoot == false)
        {
            return null;
        }

        return createItem(auditLog, fileMetadataOrNull, filterContext.UploaderUsersById);
    }

    private async Task<UploadAuditLogItem?> tryCreateRecentRootItemOrNullAsync(
        RecentRootUploadFilterContext filterContext,
        UploadAuditLog auditLog
    )
    {
        try
        {
            return await createRecentRootItemOrNullAsync(filterContext, auditLog);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GoogleApiException exception)
        {
            return handleRecentUploadValidationGoogleApiExceptionOrNull(exception, auditLog, filterContext.UploaderUsersById);
        }
        catch (HttpRequestException exception)
        {
            return handleTransientRecentUploadValidationException(exception, auditLog, filterContext.UploaderUsersById);
        }
        catch (IOException exception)
        {
            return handleTransientRecentUploadValidationException(exception, auditLog, filterContext.UploaderUsersById);
        }
    }

    private async Task<UploadAuditLogItem?> createRecentRootItemOrNullAsync(
        RecentRootUploadFilterContext filterContext,
        UploadAuditLog auditLog
    )
    {
        bool isInsideRoot = await isUploadInsideRootAsync(
            DriveItemId.CreateOptional(auditLog.FolderId),
            filterContext.ReadableRoot.FolderId,
            filterContext.CancellationToken);
        if (isInsideRoot == false)
        {
            return null;
        }

        return createItem(auditLog, filterContext.UploaderUsersById);
    }

    private UploadAuditLogItem? handleRecentUploadValidationGoogleApiExceptionOrNull(
        GoogleApiException exception,
        UploadAuditLog auditLog,
        IReadOnlyDictionary<int, UserAccount> uploaderUsersById
    )
    {
        if (isPermanentRecentUploadValidationFailure(exception))
        {
            mLogger.LogDebug(
                exception,
                "Recent upload validation found an inaccessible or stale Drive item. FileId: {FileId}, StatusCode: {StatusCode}",
                auditLog.FileId,
                exception.HttpStatusCode);
            return null;
        }

        if (isTransientRecentUploadValidationFailure(exception))
        {
            return handleTransientRecentUploadValidationException(exception, auditLog, uploaderUsersById);
        }

        mLogger.LogWarning(
            exception,
            "Recent upload validation returned an unexpected Google API error. FileId: {FileId}, StatusCode: {StatusCode}",
            auditLog.FileId,
            exception.HttpStatusCode);
        return createItem(auditLog, uploaderUsersById);
    }

    private UploadAuditLogItem handleTransientRecentUploadValidationException(
        Exception exception,
        UploadAuditLog auditLog,
        IReadOnlyDictionary<int, UserAccount> uploaderUsersById
    )
    {
        mLogger.LogDebug(
            exception,
            "Recent upload validation was temporarily unavailable. FileId: {FileId}",
            auditLog.FileId);
        return createItem(auditLog, uploaderUsersById);
    }

    private static bool isPermanentRecentUploadValidationFailure(GoogleApiException exception)
    {
        if (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.Gone)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.Forbidden)
        {
            return true;
        }

        return false;
    }

    private static bool isTransientRecentUploadValidationFailure(GoogleApiException exception)
    {
        if (exception.HttpStatusCode == HttpStatusCode.RequestTimeout)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.InternalServerError)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.BadGateway)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return true;
        }

        if (exception.HttpStatusCode == HttpStatusCode.GatewayTimeout)
        {
            return true;
        }

        return false;
    }
}
