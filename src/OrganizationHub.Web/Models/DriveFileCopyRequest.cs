using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class DriveFileCopyRequest
{
    public DriveItemId SourceFileId
    {
        get;
    }

    public DriveFolderId TargetFolderId
    {
        get;
    }

    public DriveItemName FileName
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

    private DriveFileCopyRequest(
        DriveItemId sourceFileId,
        DriveFolderId targetFolderId,
        DriveItemName fileName,
        EDriveFileNameConflictResolution conflictResolution,
        DriveItemId replacementFileId)
    {
        SourceFileId = sourceFileId;
        TargetFolderId = targetFolderId;
        FileName = fileName;
        ConflictResolution = conflictResolution;
        ReplacementFileId = replacementFileId;
    }

    public static DriveFileCopyRequest CreateNewFile(
        DriveItemId sourceFileId,
        DriveFolderId targetFolderId,
        DriveItemName fileName)
    {
        return new DriveFileCopyRequest(
            sourceFileId,
            targetFolderId,
            fileName,
            EDriveFileNameConflictResolution.AddNewFile,
            DriveItemId.Empty);
    }

    public static DriveFileCopyRequest CreateReplacementFile(
        DriveItemId sourceFileId,
        DriveFolderId targetFolderId,
        DriveItemName fileName,
        DriveItemId replacementFileId)
    {
        if (replacementFileId.HasValue == false)
        {
            throw new ArgumentException("Replacement file id is required.", nameof(replacementFileId));
        }

        return new DriveFileCopyRequest(
            sourceFileId,
            targetFolderId,
            fileName,
            EDriveFileNameConflictResolution.ReplaceExistingFile,
            replacementFileId);
    }
}
