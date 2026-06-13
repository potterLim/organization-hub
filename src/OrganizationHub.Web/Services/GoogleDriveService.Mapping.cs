using System.Collections.Generic;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
    private IReadOnlyList<DriveFileItem> createDriveFileItems(DriveFileList fileList, string teamName)
    {
        List<DriveFileItem> items = new List<DriveFileItem>();
        if (fileList.Files == null)
        {
            return items;
        }

        foreach (DriveDataFile file in fileList.Files)
        {
            DriveFileItem item = createDriveFileItem(file, teamName);
            items.Add(item);
        }

        return items;
    }

    private IReadOnlyList<DriveFileItem> cloneDriveFileItems(IReadOnlyList<DriveFileItem> files)
    {
        List<DriveFileItem> clonedFiles = new List<DriveFileItem>();
        foreach (DriveFileItem file in files)
        {
            clonedFiles.Add(cloneDriveFileItem(file));
        }

        return clonedFiles;
    }

    private DriveFileItem cloneDriveFileItem(DriveFileItem file)
    {
        DriveFileItem clonedFile = new DriveFileItem();
        clonedFile.ID = file.ID;
        clonedFile.Name = file.Name;
        clonedFile.MimeType = file.MimeType;
        clonedFile.CreatedTime = file.CreatedTime;
        clonedFile.ModifiedTime = file.ModifiedTime;
        clonedFile.IconLink = file.IconLink;
        clonedFile.ThumbnailLink = file.ThumbnailLink;
        clonedFile.IsFolder = file.IsFolder;
        clonedFile.TeamName = file.TeamName;
        clonedFile.FolderDisplayName = file.FolderDisplayName;
        clonedFile.UploadedByDisplayName = file.UploadedByDisplayName;
        clonedFile.UploadedAt = file.UploadedAt;
        clonedFile.CanPreviewInSite = file.CanPreviewInSite;
        clonedFile.Size = file.Size;
        return clonedFile;
    }

    private DriveFileItem createDriveFileItem(DriveDataFile file, string teamName)
    {
        DriveFileItem item = new DriveFileItem();
        item.ID = getNullAsEmpty(file.Id);
        item.Name = getNullAsEmpty(file.Name);
        item.MimeType = getNullAsEmpty(file.MimeType);
        item.CreatedTime = file.CreatedTimeDateTimeOffset.HasValue ? file.CreatedTimeDateTimeOffset.Value.DateTime : null;
        item.ModifiedTime = file.ModifiedTimeDateTimeOffset.HasValue ? file.ModifiedTimeDateTimeOffset.Value.DateTime : null;
        item.IconLink = getNullAsEmpty(file.IconLink);
        item.ThumbnailLink = getNullAsEmpty(file.ThumbnailLink);
        item.IsFolder = file.MimeType == "application/vnd.google-apps.folder";
        item.TeamName = teamName;
        item.CanPreviewInSite = file.MimeType == "application/pdf";
        item.Size = file.Size;
        return item;
    }

    private DriveFileMetadata createDriveFileMetadata(DriveDataFile file)
    {
        DriveFileMetadata metadata = new DriveFileMetadata();
        metadata.ID = getNullAsEmpty(file.Id);
        metadata.Name = getNullAsEmpty(file.Name);
        metadata.MimeType = getNullAsEmpty(file.MimeType);
        if (file.Parents == null)
        {
            metadata.ParentFolderIDs = new List<string>();
        }
        else
        {
            metadata.ParentFolderIDs = new List<string>(file.Parents);
        }

        return metadata;
    }

    private DriveFileMetadata cloneDriveFileMetadata(DriveFileMetadata metadata)
    {
        DriveFileMetadata clonedMetadata = new DriveFileMetadata();
        clonedMetadata.ID = metadata.ID;
        clonedMetadata.Name = metadata.Name;
        clonedMetadata.MimeType = metadata.MimeType;
        clonedMetadata.ParentFolderIDs = new List<string>(metadata.ParentFolderIDs);
        return clonedMetadata;
    }

    private string escapeDriveQueryValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }
}
