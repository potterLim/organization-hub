using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;

namespace OrganizationHub.Web.Services;

internal static class DriveDeletionConfirmation
{
    public static Task ExecuteAsync(DriveService drive, DriveFileMetadata metadata, CancellationToken cancellationToken)
    {
        DriveDataFile update = new DriveDataFile();
        update.Trashed = true;
        FilesResource.UpdateRequest request = drive.Files.Update(update, metadata.Id);
        request.Fields = "id,trashed";
        request.SupportsAllDrives = true;
        return ResourceDeletionConfirmation.ExecuteAsync(
            () => request.ExecuteAsync(cancellationToken),
            () => isDeletedAsync(drive, metadata, cancellationToken),
            cancellationToken);
    }

    private static Task<bool> isDeletedAsync(DriveService drive, DriveFileMetadata metadata, CancellationToken cancellationToken)
    {
        return ResourceDeletionConfirmation.IsDeletedAsync(
            () => isTrashedAsync(drive, metadata.Id, cancellationToken),
            () => ensureParentReadableAsync(drive, metadata, cancellationToken));
    }

    private static async Task<bool> isTrashedAsync(DriveService drive, string fileId, CancellationToken cancellationToken)
    {
        FilesResource.GetRequest request = drive.Files.Get(fileId);
        request.Fields = "id,trashed";
        request.SupportsAllDrives = true;
        DriveDataFile file = await request.ExecuteAsync(cancellationToken);
        return file.Trashed == true;
    }

    private static async Task ensureParentReadableAsync(DriveService drive, DriveFileMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata.ParentFolderIds.Count == 0)
        {
            throw new System.InvalidOperationException("A missing file cannot be confirmed without a readable parent.");
        }
        FilesResource.GetRequest request = drive.Files.Get(metadata.ParentFolderIds[0]);
        request.Fields = "id";
        request.SupportsAllDrives = true;
        await request.ExecuteAsync(cancellationToken);
    }
}
