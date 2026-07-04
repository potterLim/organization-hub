using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleHealthCheckService
{
    private async Task addFileItemsAsync(
        List<GoogleHealthCheckItem> items,
        GlobalSetting setting,
        CancellationToken cancellationToken)
    {
        await addFileItemAsync(items, "파일", "공지 파일", setting.NoticeFileId, cancellationToken);
        await addFileItemAsync(items, "파일", "업무 기준 파일", setting.WorkGuidelineFileId, cancellationToken);
    }

    private async Task addFileItemAsync(
        List<GoogleHealthCheckItem> items,
        string area,
        string name,
        string? fileIdOrNull,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileIdOrNull))
        {
            addItem(items, area, name, EGoogleHealthStatus.Warning, "파일이 연결되지 않았습니다.");
            return;
        }

        DriveFileMetadata? metadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateOptional(fileIdOrNull), cancellationToken);
        if (metadataOrNull == null)
        {
            addItem(items, area, name, EGoogleHealthStatus.Error, "연결된 계정에서 파일을 찾을 수 없습니다.");
            return;
        }

        addItem(
            items,
            area,
            name,
            EGoogleHealthStatus.Healthy,
            metadataOrNull.Name + " 파일에 접근할 수 있습니다.");
    }
}
