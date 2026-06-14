using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private async Task<DriveFileMetadata?> getOrCreateTeamParentFolderAsync(
        DriveFolderId teamRootFolderId,
        string teamDisplayName,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        DriveFileMetadata? childFolderOrNull = await getChildFolderByNameOrNullAsync(teamRootFolderId, teamDisplayName, cancellationToken);
        if (childFolderOrNull != null)
        {
            return await applyFolderNameByIdAsync(DriveFolderId.CreateRequired(childFolderOrNull.Id), teamDisplayName, syncContext, cancellationToken);
        }

        return await createTeamParentFolderAsync(teamRootFolderId, teamDisplayName, syncContext, cancellationToken);
    }

    private async Task<DriveFileMetadata?> createTeamParentFolderAsync(
        DriveFolderId teamRootFolderId,
        string teamDisplayName,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        try
        {
            DriveFileItem createdFolder = await mDriveService.CreateFolderAsync(teamRootFolderId, teamDisplayName, cancellationToken);
            syncContext.Summary.CreatedCount = syncContext.Summary.CreatedCount + 1;
            DriveFileMetadata? createdMetadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(createdFolder.Id), cancellationToken);
            if (createdMetadataOrNull != null)
            {
                return createdMetadataOrNull;
            }

            DriveFileMetadata createdMetadata = new DriveFileMetadata();
            createdMetadata.Id = createdFolder.Id;
            createdMetadata.Name = createdFolder.Name;
            createdMetadata.MimeType = GOOGLE_DRIVE_FOLDER_MIME_TYPE;
            createdMetadata.ParentFolderIds = new List<string> { teamRootFolderId.GetRequiredValue() };
            return createdMetadata;
        }
        catch (Exception exception)
        {
            syncContext.FailureMessages.Add(teamDisplayName + " 폴더를 만들지 못했습니다. " + exception.Message);
            return null;
        }
    }

    private async Task<DriveFileMetadata?> getChildFolderByNameOrNullAsync(
        DriveFolderId parentFolderId,
        string folderName,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files = await mDriveService.ListFilesAsync(parentFolderId, cancellationToken);
        DriveFileItem? folderOrNull = files.FirstOrDefault(file => file.IsFolder && file.Name == folderName);
        if (folderOrNull == null)
        {
            return null;
        }

        return await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(folderOrNull.Id), cancellationToken);
    }

    private async Task<DriveFileMetadata?> applyFolderNameIfConfiguredAsync(
        string? folderIdOrNull,
        string standardName,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderIdOrNull))
        {
            syncContext.Summary.SkippedCount = syncContext.Summary.SkippedCount + 1;
            return null;
        }

        DriveFolderId folderId = DriveFolderId.CreateRequired(folderIdOrNull);
        return await applyFolderNameByIdAsync(folderId, standardName, syncContext, cancellationToken);
    }

    private async Task<DriveFileMetadata?> applyFolderNameByIdAsync(
        DriveFolderId folderId,
        string standardName,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        string requiredFolderId = folderId.GetRequiredValue();
        if (syncContext.AppliedFolderIds.Contains(requiredFolderId))
        {
            return await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(requiredFolderId), cancellationToken);
        }

        syncContext.AppliedFolderIds.Add(requiredFolderId);
        syncContext.Summary.TargetCount = syncContext.Summary.TargetCount + 1;
        try
        {
            DriveFileMetadata? metadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(requiredFolderId), cancellationToken);
            if (metadataOrNull == null)
            {
                syncContext.FailureMessages.Add(standardName + " 폴더를 찾지 못했습니다.");
                return null;
            }

            if (metadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
            {
                syncContext.FailureMessages.Add(standardName + " 저장 대상이 폴더가 아닙니다.");
                return null;
            }

            if (metadataOrNull.Name == standardName)
            {
                syncContext.Summary.AlreadyStandardCount = syncContext.Summary.AlreadyStandardCount + 1;
                return metadataOrNull;
            }

            DriveFileMetadata renamedMetadata = await mDriveService.RenameDriveItemAsync(
                DriveItemId.CreateRequired(requiredFolderId),
                standardName,
                cancellationToken);
            syncContext.Summary.RenamedCount = syncContext.Summary.RenamedCount + 1;
            return renamedMetadata;
        }
        catch (Exception exception)
        {
            syncContext.FailureMessages.Add(standardName + " 폴더명을 변경하지 못했습니다. " + exception.Message);
            return null;
        }
    }

    private async Task moveFolderIfNeededAsync(
        DriveFileMetadata? folderMetadataOrNull,
        string? targetParentFolderIdOrNull,
        string folderDisplayName,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        if (folderMetadataOrNull == null || string.IsNullOrWhiteSpace(targetParentFolderIdOrNull))
        {
            return;
        }

        string targetParentFolderId = targetParentFolderIdOrNull.Trim();
        if (folderMetadataOrNull.ParentFolderIds.Contains(targetParentFolderId))
        {
            return;
        }

        try
        {
            await mDriveService.MoveDriveItemToFolderAsync(
                DriveItemId.CreateRequired(folderMetadataOrNull.Id),
                DriveFolderId.CreateRequired(targetParentFolderId),
                cancellationToken);
            syncContext.Summary.MovedCount = syncContext.Summary.MovedCount + 1;
        }
        catch (Exception exception)
        {
            syncContext.FailureMessages.Add(folderDisplayName + " 폴더 위치를 정리하지 못했습니다. " + exception.Message);
        }
    }

    private IReadOnlySet<string> getSharedParentFolderIds(
        DriveFileMetadata? firstMetadataOrNull,
        DriveFileMetadata? secondMetadataOrNull,
        string? officialRootFolderIdOrNull)
    {
        HashSet<string> sharedParentFolderIds = new HashSet<string>();
        if (firstMetadataOrNull == null || secondMetadataOrNull == null)
        {
            return sharedParentFolderIds;
        }

        HashSet<string> secondParentFolderIds = new HashSet<string>(secondMetadataOrNull.ParentFolderIds);
        foreach (string parentFolderId in firstMetadataOrNull.ParentFolderIds)
        {
            if (secondParentFolderIds.Contains(parentFolderId) && isOfficialRootFolderId(parentFolderId, officialRootFolderIdOrNull) == false)
            {
                sharedParentFolderIds.Add(parentFolderId);
            }
        }

        return sharedParentFolderIds;
    }

    private bool isOfficialRootFolderId(string folderId, string? officialRootFolderIdOrNull)
    {
        return string.IsNullOrWhiteSpace(officialRootFolderIdOrNull) == false && folderId == officialRootFolderIdOrNull.Trim();
    }
}
