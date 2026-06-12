using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Tests.Fakes;

public sealed class FakeDriveService : IDriveService
{
    private readonly Dictionary<string, DriveFileMetadata> mMetadataByFileID = new Dictionary<string, DriveFileMetadata>();
    private readonly Dictionary<string, IReadOnlySet<string>> mAncestorFolderIDsByItemID = new Dictionary<string, IReadOnlySet<string>>();

    public void AddFolderAncestors(string folderID, params string[] ancestorFolderIDs)
    {
        mAncestorFolderIDsByItemID[folderID] = new HashSet<string>(ancestorFolderIDs);
    }

    public void AddFolder(string folderID, string folderName, params string[] ancestorFolderIDs)
    {
        AddFile(folderID, folderName, "application/vnd.google-apps.folder", ancestorFolderIDs);
    }

    public void AddFile(string fileID, string fileName, string mimeType, params string[] ancestorFolderIDs)
    {
        DriveFileMetadata metadata = new DriveFileMetadata();
        metadata.ID = fileID;
        metadata.Name = fileName;
        metadata.MimeType = mimeType;
        metadata.ParentFolderIDs = new List<string>(ancestorFolderIDs);
        mMetadataByFileID[fileID] = metadata;
        mAncestorFolderIDsByItemID[fileID] = new HashSet<string>(ancestorFolderIDs);
    }

    public Task<IReadOnlyList<DriveFileItem>> ListFilesAsync(string folderID, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files = new List<DriveFileItem>();
        return Task.FromResult(files);
    }

    public Task<IReadOnlyList<DriveFileItem>> SearchFilesAsync(string folderID, string keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files = new List<DriveFileItem>();
        return Task.FromResult(files);
    }

    public Task<DriveFileMetadata?> GetFileOrNullAsync(string fileID, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        mMetadataByFileID.TryGetValue(fileID, out metadataOrNull);
        return Task.FromResult(metadataOrNull);
    }

    public Task<IReadOnlySet<string>> GetAncestorFolderIDsAsync(string fileID, CancellationToken cancellationToken)
    {
        IReadOnlySet<string>? ancestorFolderIDsOrNull;
        if (mAncestorFolderIDsByItemID.TryGetValue(fileID, out ancestorFolderIDsOrNull) == false)
        {
            IReadOnlySet<string> emptyAncestorFolderIDs = new HashSet<string>();
            return Task.FromResult(emptyAncestorFolderIDs);
        }

        IReadOnlySet<string> ancestorFolderIDs = new HashSet<string>(ancestorFolderIDsOrNull);
        return Task.FromResult(ancestorFolderIDs);
    }

    public Task<DriveFileMetadata> RenameDriveItemAsync(string itemID, string name, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        if (mMetadataByFileID.TryGetValue(itemID, out metadataOrNull) == false || metadataOrNull == null)
        {
            throw new InvalidOperationException("Drive item was not found.");
        }

        metadataOrNull.Name = name;
        return Task.FromResult(metadataOrNull);
    }

    public Task<DriveFileMetadata> MoveDriveItemToFolderAsync(string itemID, string targetFolderID, CancellationToken cancellationToken)
    {
        DriveFileMetadata? metadataOrNull;
        if (mMetadataByFileID.TryGetValue(itemID, out metadataOrNull) == false || metadataOrNull == null)
        {
            throw new InvalidOperationException("Drive item was not found.");
        }

        List<string> ancestorFolderIDs = new List<string>();
        ancestorFolderIDs.Add(targetFolderID);
        IReadOnlySet<string>? targetAncestorFolderIDsOrNull;
        if (mAncestorFolderIDsByItemID.TryGetValue(targetFolderID, out targetAncestorFolderIDsOrNull))
        {
            ancestorFolderIDs.AddRange(targetAncestorFolderIDsOrNull);
        }

        metadataOrNull.ParentFolderIDs = ancestorFolderIDs;
        mAncestorFolderIDsByItemID[itemID] = new HashSet<string>(ancestorFolderIDs);
        return Task.FromResult(metadataOrNull);
    }

    public Task<DriveFileItem> UploadFileAsync(string targetFolderID, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.ID = "uploaded-file";
        driveFileItem.Name = fileName;
        driveFileItem.MimeType = contentType;
        return Task.FromResult(driveFileItem);
    }

    public Task<DriveFileItem> CreateFolderAsync(string parentFolderID, string folderName, CancellationToken cancellationToken)
    {
        string folderID = "folder-" + folderName;
        List<string> ancestorFolderIDs = new List<string>();
        ancestorFolderIDs.Add(parentFolderID);
        IReadOnlySet<string>? parentAncestorFolderIDsOrNull;
        if (mAncestorFolderIDsByItemID.TryGetValue(parentFolderID, out parentAncestorFolderIDsOrNull))
        {
            ancestorFolderIDs.AddRange(parentAncestorFolderIDsOrNull);
        }

        AddFolder(folderID, folderName, ancestorFolderIDs.ToArray());
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.ID = folderID;
        driveFileItem.Name = folderName;
        driveFileItem.MimeType = "application/vnd.google-apps.folder";
        driveFileItem.IsFolder = true;
        return Task.FromResult(driveFileItem);
    }

    public Task<DriveFileItem> CopyFileToFolderAsync(string fileID, string targetFolderID, string fileName, CancellationToken cancellationToken)
    {
        string copiedFileID = "copied-" + fileID;
        List<string> ancestorFolderIDs = new List<string>();
        ancestorFolderIDs.Add(targetFolderID);
        IReadOnlySet<string>? targetAncestorFolderIDsOrNull;
        if (mAncestorFolderIDsByItemID.TryGetValue(targetFolderID, out targetAncestorFolderIDsOrNull))
        {
            ancestorFolderIDs.AddRange(targetAncestorFolderIDsOrNull);
        }

        AddFile(copiedFileID, fileName, "application/pdf", ancestorFolderIDs.ToArray());
        DriveFileItem driveFileItem = new DriveFileItem();
        driveFileItem.ID = copiedFileID;
        driveFileItem.Name = fileName;
        driveFileItem.MimeType = "application/pdf";
        return Task.FromResult(driveFileItem);
    }

    public Task DeleteDriveItemAsync(string itemID, CancellationToken cancellationToken)
    {
        mMetadataByFileID.Remove(itemID);
        mAncestorFolderIDsByItemID.Remove(itemID);
        return Task.CompletedTask;
    }

    public Task<FileDownloadResult> DownloadFileAsync(string fileID, CancellationToken cancellationToken)
    {
        MemoryStream contentStream = new MemoryStream();
        FileDownloadResult downloadResult = new FileDownloadResult(contentStream, fileID + ".pdf", "application/pdf");
        return Task.FromResult(downloadResult);
    }
}
