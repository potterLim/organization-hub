using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.BrowserFixture;

public class CaptureDriveProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "DeleteDriveItemAsync" && CaptureState.Mode == "drive-delete-uncertain")
        {
            Type uncertaintyType = typeof(IDriveService).Assembly.GetType("OrganizationHub.Web.Services.ResourceDeletionUncertainException")!;
            Exception uncertainty = (Exception)Activator.CreateInstance(uncertaintyType, new IOException("Synthetic deletion state cannot be read."))!;
            return Task.FromException(uncertainty);
        }
        if (method!.Name == "ListFilesAsync" || method.Name == "SearchFilesAsync")
        {
            return listAsync(method.Name, args!);
        }

        if (method.Name == "DownloadFileAsync")
        {
            return CaptureState.Drive.DownloadFileSnapshotAsync((DriveItemId)args![0]!, CancellationToken.None);
        }

        return method.Invoke(CaptureState.Drive, args);
    }
    private static async Task<IReadOnlyList<DriveFileItem>> listAsync(string method, object?[] args)
    {
        if (CaptureState.Mode == "loading")
        {
            await Task.Delay(3500);
        }

        if (CaptureState.Mode == "error")
        {
            throw new IOException("캡처용 외부 서비스 응답 지연");
        }

        if (CaptureState.Mode == "empty")
        {
            return Array.Empty<DriveFileItem>();
        }

        IReadOnlyList<DriveFileItem> items = await CaptureState.Drive.ListFilesAsync((DriveFolderId)args[0]!, CancellationToken.None);
        if (CaptureState.Mode == "approval-multiple" && ((DriveFolderId)args[0]!).GetRequiredValue() == "qa-approved")
        {
            List<DriveFileItem> duplicates = items.ToList();
            DriveFileItem duplicate = new DriveFileItem();
            duplicate.Id = "duplicate-approved";
            duplicate.Name = "2026-10-19_경영지원팀_분기운영계획.pdf";
            duplicates.Add(duplicate);
            items = duplicates;
        }
        if (method == "SearchFilesAsync")
        {
            string keyword = ((SearchKeyword)args[1]!).GetOptionalValue();
            items = items.Where(item => item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        foreach (DriveFileItem item in items)
        {
            item.ModifiedTime = new DateTime(2026, 10, 19, 2, 30, 0, DateTimeKind.Utc);
            item.CreatedTime = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
            item.Size = item.IsFolder ? null : CaptureState.DocumentContent.Length;
            item.CanPreviewInSite = item.MimeType == "application/pdf";
        }
        return items;
    }
}
