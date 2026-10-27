using System;
using System.IO;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class DriveFileUploadRequest
{
    public DriveItemId ReservedFileId
    {
        get; private init;
    }
    public string OperationKey { get; private init; } = string.Empty;

    public DriveFolderId TargetFolderId
    {
        get;
    }

    public Stream ContentStream
    {
        get;
    }

    public DriveItemName FileName
    {
        get;
    }

    public FileContentType ContentType
    {
        get;
    }

    public EDriveFileNameConflictResolution ConflictResolution
    {
        get;
    }

    public DriveItemId ReplacementFileId
    {
        get;
    }

    private DriveFileUploadRequest(
        DriveFolderId targetFolderId,
        Stream contentStream,
        DriveItemName fileName,
        FileContentType contentType,
        EDriveFileNameConflictResolution conflictResolution,
        DriveItemId replacementFileId)
    {
        TargetFolderId = targetFolderId;
        ContentStream = contentStream;
        FileName = fileName;
        ContentType = contentType;
        ConflictResolution = conflictResolution;
        ReplacementFileId = replacementFileId;
    }

    public static DriveFileUploadRequest CreateNewFile(
        DriveFolderId targetFolderId,
        Stream contentStream,
        DriveItemName fileName,
        FileContentType contentType)
    {
        return new DriveFileUploadRequest(
            targetFolderId,
            contentStream,
            fileName,
            contentType,
            EDriveFileNameConflictResolution.AddNewFile,
            DriveItemId.Empty);
    }

    public DriveFileUploadRequest WithUploadIdentity(DriveItemId reservedFileId, string operationKey)
    {
        reservedFileId.GetRequiredValue();
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            throw new ArgumentException("An upload operation key is required.", nameof(operationKey));
        }
        return new DriveFileUploadRequest(TargetFolderId, ContentStream, FileName, ContentType, ConflictResolution, ReplacementFileId)
        {
            ReservedFileId = reservedFileId,
            OperationKey = operationKey
        };
    }

    public static DriveFileUploadRequest CreateReplacementFile(
        DriveFolderId targetFolderId,
        Stream contentStream,
        DriveItemName fileName,
        FileContentType contentType,
        DriveItemId replacementFileId)
    {
        if (replacementFileId.HasValue == false)
        {
            throw new ArgumentException("Replacement file id is required.", nameof(replacementFileId));
        }

        return new DriveFileUploadRequest(
            targetFolderId,
            contentStream,
            fileName,
            contentType,
            EDriveFileNameConflictResolution.ReplaceExistingFile,
            replacementFileId);
    }
}
