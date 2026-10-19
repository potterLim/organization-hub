using System;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class FileUploadCompletion
{
    public DriveFileItem File
    {
        get;
    }
    public bool IsAuditRecorded
    {
        get;
    }

    private FileUploadCompletion(DriveFileItem file, bool isAuditRecorded)
    {
        File = file;
        IsAuditRecorded = isAuditRecorded;
    }

    public static async Task<FileUploadCompletion> ExecuteAsync(Func<Task<DriveFileItem>> uploadAsync, Func<DriveFileItem, Task> recordAuditAsync, Action<Exception> logAuditFailure)
    {
        DriveFileItem file = await uploadAsync();
        try
        {
            await recordAuditAsync(file);
            return new FileUploadCompletion(file, true);
        }
        catch (Exception exception)
        {
            logAuditFailure(exception);
            return new FileUploadCompletion(file, false);
        }
    }
}
