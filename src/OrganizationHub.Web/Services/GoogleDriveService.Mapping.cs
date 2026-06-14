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
        clonedFile.Id = file.Id;
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
        item.Id = getTextOrEmpty(file.Id);
        item.Name = getTextOrEmpty(file.Name);
        item.MimeType = getTextOrEmpty(file.MimeType);
        item.CreatedTime = file.CreatedTimeDateTimeOffset.HasValue ? file.CreatedTimeDateTimeOffset.Value.DateTime : null;
        item.ModifiedTime = file.ModifiedTimeDateTimeOffset.HasValue ? file.ModifiedTimeDateTimeOffset.Value.DateTime : null;
        item.IconLink = getTextOrEmpty(file.IconLink);
        item.ThumbnailLink = getTextOrEmpty(file.ThumbnailLink);
        item.IsFolder = file.MimeType == "application/vnd.google-apps.folder";
        item.TeamName = teamName;
        item.CanPreviewInSite = file.MimeType == "application/pdf";
        item.Size = file.Size;
        return item;
    }

    private DriveFileMetadata createDriveFileMetadata(DriveDataFile file)
    {
        DriveFileMetadata metadata = new DriveFileMetadata();
        metadata.Id = getTextOrEmpty(file.Id);
        metadata.Name = getTextOrEmpty(file.Name);
        metadata.MimeType = getTextOrEmpty(file.MimeType);
        if (file.Parents == null)
        {
            metadata.ParentFolderIds = new List<string>();
        }
        else
        {
            metadata.ParentFolderIds = new List<string>(file.Parents);
        }

        return metadata;
    }

    private DriveFileMetadata cloneDriveFileMetadata(DriveFileMetadata metadata)
    {
        DriveFileMetadata clonedMetadata = new DriveFileMetadata();
        clonedMetadata.Id = metadata.Id;
        clonedMetadata.Name = metadata.Name;
        clonedMetadata.MimeType = metadata.MimeType;
        clonedMetadata.ParentFolderIds = new List<string>(metadata.ParentFolderIds);
        return clonedMetadata;
    }

    private string escapeDriveQueryValue(string queryValue)
    {
        return queryValue.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }
}
