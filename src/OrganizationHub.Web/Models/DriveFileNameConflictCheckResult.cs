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

    public bool CanReplaceExistingFile
    {
        get
        {
            return ExistingFiles.Count == 1;
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

    public DriveItemId GetSingleExistingFileId()
    {
        if (CanReplaceExistingFile == false)
        {
            throw new InvalidOperationException("같은 이름의 파일이 여러 개이거나 대상이 바뀌었습니다. 새 파일로 추가하거나 자료실에서 교체할 파일을 확인해 주세요.");
        }

        return DriveItemId.CreateRequired(ExistingFiles[0].Id);
    }
}
