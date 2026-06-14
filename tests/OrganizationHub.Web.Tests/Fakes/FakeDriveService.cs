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
            .Where(metadata => metadata.ParentFolderIds.Count > 0 && metadata.ParentFolderIds[0] == folderId.Value)
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

    public Task<DriveFileMetadata?> GetFileOrNullAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        mMetadataByFileId.TryGetValue(fileId.Value, out metadataOrNull);
        return Task.FromResult(metadataOrNull);
    }

    public Task<IReadOnlySet<DriveFolderId>> GetAncestorFolderIdsAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        IReadOnlySet<string>? ancestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(fileId.Value, out ancestorFolderIdsOrNull) == false)
        {
            IReadOnlySet<DriveFolderId> emptyAncestorFolderIds = new HashSet<DriveFolderId>();
            return Task.FromResult(emptyAncestorFolderIds);
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = ancestorFolderIdsOrNull.Select(ancestorFolderId => DriveFolderId.CreateOptional(ancestorFolderId)).ToHashSet();
        return Task.FromResult(ancestorFolderIds);
    }

    public Task<DriveFileMetadata> RenameDriveItemAsync(DriveItemId itemId, string name, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        if (mMetadataByFileId.TryGetValue(itemId.Value, out metadataOrNull) == false || metadataOrNull == null)
        {
            throw new InvalidOperationException("Drive item was not found.");
        }

        metadataOrNull.Name = name;
        return Task.FromResult(metadataOrNull);
    }

    public Task<DriveFileMetadata> MoveDriveItemToFolderAsync(DriveItemId itemId, DriveFolderId targetFolderId, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        if (mMetadataByFileId.TryGetValue(itemId.Value, out metadataOrNull) == false || metadataOrNull == null)
        {
            throw new InvalidOperationException("Drive item was not found.");
        }

        List<string> ancestorFolderIds = new List<string>();
        ancestorFolderIds.Add(targetFolderId.Value);
        IReadOnlySet<string>? targetAncestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(targetFolderId.Value, out targetAncestorFolderIdsOrNull))
        {
            ancestorFolderIds.AddRange(targetAncestorFolderIdsOrNull);
        }

        metadataOrNull.ParentFolderIds = ancestorFolderIds;
        mAncestorFolderIdsByItemId[itemId.Value] = new HashSet<string>(ancestorFolderIds);
        return Task.FromResult(metadataOrNull);
    }

    public Task<DriveFileItem> UploadFileAsync(DriveFolderId targetFolderId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.Id = "uploaded-file";
        driveFileItem.Name = fileName;
        driveFileItem.MimeType = contentType;
        return Task.FromResult(driveFileItem);
    }

    public Task<DriveFileItem> CreateFolderAsync(DriveFolderId parentFolderId, string folderName, CancellationToken cancellationToken)
    {
        string folderId = createFolderId(parentFolderId.Value, folderName);
        List<string> ancestorFolderIds = new List<string>();
        ancestorFolderIds.Add(parentFolderId.Value);
        IReadOnlySet<string>? parentAncestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(parentFolderId.Value, out parentAncestorFolderIdsOrNull))
        {
            ancestorFolderIds.AddRange(parentAncestorFolderIdsOrNull);
        }

        AddFolder(folderId, folderName, ancestorFolderIds.ToArray());
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.Id = folderId;
        driveFileItem.Name = folderName;
        driveFileItem.MimeType = "application/vnd.google-apps.folder";
        driveFileItem.IsFolder = true;
        return Task.FromResult(driveFileItem);
    }

    public Task<DriveFileItem> CopyFileToFolderAsync(DriveItemId fileId, DriveFolderId targetFolderId, string fileName, CancellationToken cancellationToken)
    {
        string copiedFileId = "copied-" + fileId.Value;
        List<string> ancestorFolderIds = new List<string>();
        ancestorFolderIds.Add(targetFolderId.Value);
        IReadOnlySet<string>? targetAncestorFolderIdsOrNull;
        if (mAncestorFolderIdsByItemId.TryGetValue(targetFolderId.Value, out targetAncestorFolderIdsOrNull))
        {
            ancestorFolderIds.AddRange(targetAncestorFolderIdsOrNull);
        }

        AddFile(copiedFileId, fileName, "application/pdf", ancestorFolderIds.ToArray());
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.Id = copiedFileId;
        driveFileItem.Name = fileName;
        driveFileItem.MimeType = "application/pdf";
        return Task.FromResult(driveFileItem);
    }

    public Task DeleteDriveItemAsync(DriveItemId itemId, CancellationToken cancellationToken)
    {
        mMetadataByFileId.Remove(itemId.Value);
        mAncestorFolderIdsByItemId.Remove(itemId.Value);
        return Task.CompletedTask;
    }

    public Task<FileDownloadResult> DownloadFileAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        MemoryStream contentStream = new MemoryStream();
        FileDownloadResult downloadResult = new FileDownloadResult(contentStream, fileId.Value + ".pdf", "application/pdf");
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
}
