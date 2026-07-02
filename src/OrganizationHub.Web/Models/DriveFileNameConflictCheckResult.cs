using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class DriveFileNameConflictCheckResult
{
    public DriveItemName RequestedFileName
    {
        get;
    }

    public DriveItemName SuggestedNewFileName
    {
        get;
    }

    public IReadOnlyList<DriveFileItem> ExistingFiles
    {
        get;
    }

    public bool HasConflict
    {
        get
        {
            return ExistingFiles.Count > 0;
        }
    }

    public DriveFileNameConflictCheckResult(
        DriveItemName requestedFileName,
        DriveItemName suggestedNewFileName,
        IReadOnlyList<DriveFileItem> existingFiles)
    {
        RequestedFileName = requestedFileName;
        SuggestedNewFileName = suggestedNewFileName;
        if (existingFiles == null)
        {
            throw new ArgumentNullException(nameof(existingFiles));
        }

        ExistingFiles = existingFiles;
    }

    public DriveItemId GetFirstExistingFileId()
    {
        if (HasConflict == false)
        {
            throw new InvalidOperationException("File name conflict result does not contain an existing file.");
        }

        return DriveItemId.CreateRequired(ExistingFiles[0].Id);
    }
}
