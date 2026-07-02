using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Tests.Fakes;

public sealed class FakeDriveService : IDriveService
{
    private readonly Dictionary<string, DriveFileMetadata> mMetadataByFileId = new Dictionary<string, DriveFileMetadata>();
    private readonly Dictionary<string, IReadOnlySet<string>> mAncestorFolderIdsByItemId = new Dictionary<string, IReadOnlySet<string>>();

    public void AddFolderAncestors(string folderId, params string[] ancestorFolderIds)
    {
        mAncestorFolderIdsByItemId[folderId] = new HashSet<string>(ancestorFolderIds);
    }

    public void AddFolder(string folderId, string folderName, params string[] ancestorFolderIds)
    {
        AddFile(folderId, folderName, "application/vnd.google-apps.folder", ancestorFolderIds);
    }

    public void AddFile(string fileId, string fileName, string mimeType, params string[] ancestorFolderIds)
    {
        DriveFileMetadata metadata = new DriveFileMetadata();
        metadata.Id = fileId;
        metadata.Name = fileName;
        metadata.MimeType = mimeType;
        metadata.ParentFolderIds = new List<string>(ancestorFolderIds);
        mMetadataByFileId[fileId] = metadata;
        mAncestorFolderIdsByItemId[fileId] = new HashSet<string>(ancestorFolderIds);
    }

    public Task<IReadOnlyList<DriveFileItem>> ListFilesAsync(DriveFolderId folderId, CancellationToken cancellationToken)
    {
        List<DriveFileItem> files = mMetadataByFileId.Values
            .Where(metadata => metadata.ParentFolderIds.Count > 0 && metadata.ParentFolderIds[0] == folderId.GetRequiredValue())
            .Select(createDriveFileItem)
            .OrderBy(file => file.Name)
            .ToList();
        return Task.FromResult<IReadOnlyList<DriveFileItem>>(files);
    }

    public Task<IReadOnlyList<DriveFileItem>> SearchFilesAsync(DriveFolderId folderId, SearchKeyword keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files = new List<DriveFileItem>();
        return Task.FromResult(files);
    }

    public async Task<DriveFileNameConflictCheckResult> CheckFileNameConflictAsync(DriveFolderId folderId, DriveItemName fileName, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> existingItems = await ListFilesAsync(folderId, cancellationToken);
        List<DriveFileItem> conflictingFiles = existingItems
            .Where(item => item.IsFolder == false && string.Equals(item.Name, fileName.GetRequiredValue(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        DriveItemName suggestedFileName = DriveFileNamePolicy.CreateAvailableFileName(fileName, existingItems);
        DriveFileNameConflictCheckResult result = new DriveFileNameConflictCheckResult(fileName, suggestedFileName, conflictingFiles);
        return result;
    }

    public Task<DriveFileMetadata?> GetFileOrNullAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        mMetadataByFileId.TryGetValue(fileId.GetRequiredValue(), out metadataOrNull);
        return Task.FromResult(metadataOrNull);
    }

    public Task<IReadOnlySet<DriveFolderId>> GetAncestorFolderIdsAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        IReadOnlySet<string>? ancestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(fileId.GetRequiredValue(), out ancestorFolderIdsOrNull) == false)
        {
            IReadOnlySet<DriveFolderId> emptyAncestorFolderIds = new HashSet<DriveFolderId>();
            return Task.FromResult(emptyAncestorFolderIds);
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = ancestorFolderIdsOrNull.Select(ancestorFolderId => DriveFolderId.CreateOptional(ancestorFolderId)).ToHashSet();
        return Task.FromResult(ancestorFolderIds);
    }

    public Task<DriveFileMetadata> RenameDriveItemAsync(DriveItemId itemId, DriveItemName itemName, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        if (mMetadataByFileId.TryGetValue(itemId.GetRequiredValue(), out metadataOrNull) == false || metadataOrNull == null)
        {
            throw new InvalidOperationException("Drive item was not found.");
        }

        metadataOrNull.Name = itemName.GetRequiredValue();
        return Task.FromResult(metadataOrNull);
    }

    public Task<DriveFileMetadata> MoveDriveItemToFolderAsync(DriveItemId itemId, DriveFolderId targetFolderId, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        string requiredItemId = itemId.GetRequiredValue();
        string requiredTargetFolderId = targetFolderId.GetRequiredValue();
        if (mMetadataByFileId.TryGetValue(requiredItemId, out metadataOrNull) == false || metadataOrNull == null)
        {
            throw new InvalidOperationException("Drive item was not found.");
        }

        List<string> ancestorFolderIds = new List<string>();
        ancestorFolderIds.Add(requiredTargetFolderId);
        IReadOnlySet<string>? targetAncestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(requiredTargetFolderId, out targetAncestorFolderIdsOrNull))
        {
            ancestorFolderIds.AddRange(targetAncestorFolderIdsOrNull);
        }

        metadataOrNull.ParentFolderIds = ancestorFolderIds;
        mAncestorFolderIdsByItemId[requiredItemId] = new HashSet<string>(ancestorFolderIds);
        return Task.FromResult(metadataOrNull);
    }

    public async Task<DriveFileItem> UploadFileAsync(DriveFileUploadRequest request, CancellationToken cancellationToken)
    {
        if (request.ConflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile)
        {
            string replacementFileId = request.ReplacementFileId.GetRequiredValue();
            DriveFileMetadata metadata = mMetadataByFileId[replacementFileId];
            metadata.Name = request.FileName.GetRequiredValue();
            DriveFileItem replacedFile = createDriveFileItem(metadata);
            return replacedFile;
        }

        DriveFileNameConflictCheckResult conflictCheckResult = await CheckFileNameConflictAsync(request.TargetFolderId, request.FileName, cancellationToken);
        DriveItemName uploadFileName = request.FileName;
        if (request.ConflictResolution == EDriveFileNameConflictResolution.AddNewFile && conflictCheckResult.HasConflict)
        {
            uploadFileName = conflictCheckResult.SuggestedNewFileName;
        }

        string requiredTargetFolderId = request.TargetFolderId.GetRequiredValue();
        string requiredFileName = uploadFileName.GetRequiredValue();
        string requiredContentType = request.ContentType.GetRequiredValue();
        string fileId = createFileId(requiredTargetFolderId, requiredFileName);
        List<string> ancestorFolderIds = new List<string>();
        ancestorFolderIds.Add(requiredTargetFolderId);
        IReadOnlySet<string>? parentAncestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(requiredTargetFolderId, out parentAncestorFolderIdsOrNull))
        {
            ancestorFolderIds.AddRange(parentAncestorFolderIdsOrNull);
        }

        AddFile(fileId, requiredFileName, requiredContentType, ancestorFolderIds.ToArray());
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.Id = fileId;
        driveFileItem.Name = requiredFileName;
        driveFileItem.MimeType = requiredContentType;
        return driveFileItem;
    }

    public Task<DriveFileItem> CreateFolderAsync(DriveFolderId parentFolderId, DriveItemName folderName, CancellationToken cancellationToken)
    {
        string requiredParentFolderId = parentFolderId.GetRequiredValue();
        string requiredFolderName = folderName.GetRequiredValue();
        string folderId = createFolderId(requiredParentFolderId, requiredFolderName);
        List<string> ancestorFolderIds = new List<string>();
        ancestorFolderIds.Add(requiredParentFolderId);
        IReadOnlySet<string>? parentAncestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(requiredParentFolderId, out parentAncestorFolderIdsOrNull))
        {
            ancestorFolderIds.AddRange(parentAncestorFolderIdsOrNull);
        }

        AddFolder(folderId, requiredFolderName, ancestorFolderIds.ToArray());
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.Id = folderId;
        driveFileItem.Name = requiredFolderName;
        driveFileItem.MimeType = "application/vnd.google-apps.folder";
        driveFileItem.IsFolder = true;
        return Task.FromResult(driveFileItem);
    }

    public async Task<DriveFileItem> CopyFileToFolderAsync(DriveFileCopyRequest request, CancellationToken cancellationToken)
    {
        if (request.ConflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile)
        {
            string replacementFileId = request.ReplacementFileId.GetRequiredValue();
            DriveFileMetadata metadata = mMetadataByFileId[replacementFileId];
            metadata.Name = request.FileName.GetRequiredValue();
            DriveFileItem replacedFile = createDriveFileItem(metadata);
            return replacedFile;
        }

        DriveFileNameConflictCheckResult conflictCheckResult = await CheckFileNameConflictAsync(request.TargetFolderId, request.FileName, cancellationToken);
        DriveItemName copyFileName = request.FileName;
        if (request.ConflictResolution == EDriveFileNameConflictResolution.AddNewFile && conflictCheckResult.HasConflict)
        {
            copyFileName = conflictCheckResult.SuggestedNewFileName;
        }

        string requiredFileId = request.SourceFileId.GetRequiredValue();
        string requiredTargetFolderId = request.TargetFolderId.GetRequiredValue();
        string requiredFileName = copyFileName.GetRequiredValue();
        string copiedFileId = "copied-" + requiredFileId;
        List<string> ancestorFolderIds = new List<string>();
        ancestorFolderIds.Add(requiredTargetFolderId);
        IReadOnlySet<string>? targetAncestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(requiredTargetFolderId, out targetAncestorFolderIdsOrNull))
        {
            ancestorFolderIds.AddRange(targetAncestorFolderIdsOrNull);
        }

        AddFile(copiedFileId, requiredFileName, "application/pdf", ancestorFolderIds.ToArray());
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.Id = copiedFileId;
        driveFileItem.Name = requiredFileName;
        driveFileItem.MimeType = "application/pdf";
        return driveFileItem;
    }

    public Task DeleteDriveItemAsync(DriveItemId itemId, CancellationToken cancellationToken)
    {
        string requiredItemId = itemId.GetRequiredValue();
        mMetadataByFileId.Remove(requiredItemId);
        mAncestorFolderIdsByItemId.Remove(requiredItemId);
        return Task.CompletedTask;
    }

    public Task<FileDownloadResult> DownloadFileAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        MemoryStream contentStream = new MemoryStream();
        FileDownloadResult downloadResult = new FileDownloadResult(contentStream, DriveItemName.CreateRequired(fileId.GetRequiredValue() + ".pdf"), FileContentType.CreateOptional("application/pdf"));
        return Task.FromResult(downloadResult);
    }

    private DriveFileItem createDriveFileItem(DriveFileMetadata metadata)
    {
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.Id = metadata.Id;
        driveFileItem.Name = metadata.Name;
        driveFileItem.MimeType = metadata.MimeType;
        driveFileItem.IsFolder = metadata.MimeType == "application/vnd.google-apps.folder";
        return driveFileItem;
    }

    private string createFolderId(string parentFolderId, string folderName)
    {
        string folderId = "folder-" + folderName;
        if (mMetadataByFileId.ContainsKey(folderId) == false)
        {
            return folderId;
        }

        folderId = "folder-" + parentFolderId + "-" + folderName;
        int suffix = 2;
        while (mMetadataByFileId.ContainsKey(folderId))
        {
            folderId = "folder-" + parentFolderId + "-" + folderName + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            suffix = suffix + 1;
        }

        return folderId;
    }

    private string createFileId(string parentFolderId, string fileName)
    {
        string safeFileName = fileName.Replace(" ", "-", StringComparison.Ordinal);
        string fileId = "file-" + safeFileName;
        if (mMetadataByFileId.ContainsKey(fileId) == false)
        {
            return fileId;
        }

        fileId = "file-" + parentFolderId + "-" + safeFileName;
        int suffix = 2;
        while (mMetadataByFileId.ContainsKey(fileId))
        {
            fileId = "file-" + parentFolderId + "-" + safeFileName + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            suffix = suffix + 1;
        }

        return fileId;
    }
}
