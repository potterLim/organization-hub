using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Drive.v3;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;

namespace OrganizationHub.Web.Services;

internal static class DriveUploadReceipt
{
    private const string OPERATION_PROPERTY = "organizationHubUpload";

    public static void Apply(DriveDataFile metadata, DriveFileUploadRequest request)
    {
        if (request.OperationKey.Length == 0)
        {
            return;
        }
        metadata.AppProperties = new Dictionary<string, string> { { OPERATION_PROPERTY, request.OperationKey } };
    }

    public static async Task<DriveDataFile?> GetFileOrNullAsync(DriveService drive, string fileId, string operationKey, CancellationToken cancellationToken)
    {
        FilesResource.GetRequest request = drive.Files.Get(fileId);
        request.Fields = "id,name,mimeType,parents,appProperties,trashed";
        request.SupportsAllDrives = true;
        try
        {
            DriveDataFile file = await request.ExecuteAsync(cancellationToken);
            string? storedKeyOrNull;
            if (file.Trashed == true || file.AppProperties == null || file.AppProperties.TryGetValue(OPERATION_PROPERTY, out storedKeyOrNull) == false || storedKeyOrNull != operationKey)
            {
                return null;
            }
            return file;
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
