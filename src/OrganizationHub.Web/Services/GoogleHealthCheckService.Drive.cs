using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleHealthCheckService
{
    private async Task addFolderItemsAsync(
        List<GoogleHealthCheckItem> items,
        GlobalSetting setting,
        List<Team> teams,
        CancellationToken cancellationToken)
    {
        string officialRootFolderId = getTextOrEmpty(setting.OfficialRootFolderId);
        await addFolderItemAsync(
            items,
            createFolderHealthCheckRequest(
                "Drive",
                "기본 자료 위치",
                setting.OfficialRootFolderId,
                string.Empty,
                EGoogleHealthFolderCheckKind.RootFolder),
            cancellationToken);
        await addFolderItemAsync(
            items,
            createFolderHealthCheckRequest(
                "Drive",
                OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME,
                setting.PublicFolderId,
                officialRootFolderId,
                EGoogleHealthFolderCheckKind.ChildFolder),
            cancellationToken);
        await addFolderItemAsync(
            items,
            createFolderHealthCheckRequest(
                "Drive",
                OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME,
                setting.CommonFolderId,
                officialRootFolderId,
                EGoogleHealthFolderCheckKind.ChildFolder),
            cancellationToken);
        await addFolderItemAsync(
            items,
            createFolderHealthCheckRequest(
                "Drive",
                OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME,
                setting.ResponsibleResourcesFolderId,
                officialRootFolderId,
                EGoogleHealthFolderCheckKind.ChildFolder),
            cancellationToken);
        await addFolderItemAsync(
            items,
            createFolderHealthCheckRequest(
                "Drive",
                OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME,
                setting.WorkResourcesFolderId,
                officialRootFolderId,
                EGoogleHealthFolderCheckKind.ChildFolder),
            cancellationToken);

        foreach (Team team in teams)
        {
            await addTeamFolderItemsAsync(items, team, officialRootFolderId, cancellationToken);
        }
    }

    private async Task addTeamFolderItemsAsync(
        List<GoogleHealthCheckItem> items,
        Team team,
        string officialRootFolderId,
        CancellationToken cancellationToken)
    {
        await addFolderItemAsync(
            items,
            createFolderHealthCheckRequest(
                "팀 Drive",
                team.DisplayName + " 자료",
                team.MaterialsFolderId,
                officialRootFolderId,
                EGoogleHealthFolderCheckKind.ChildFolder),
            cancellationToken);
        await addFolderItemAsync(
            items,
            createFolderHealthCheckRequest(
                "팀 Drive",
                team.DisplayName + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME,
                team.ApprovedFolderId,
                officialRootFolderId,
                EGoogleHealthFolderCheckKind.ChildFolder),
            cancellationToken);
    }

    private async Task addFolderItemAsync(
        List<GoogleHealthCheckItem> items,
        GoogleFolderHealthCheckRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FolderIdOrNull))
        {
            addItem(items, request.Area, request.Name, EGoogleHealthStatus.Warning, "폴더가 연결되지 않았습니다.");
            return;
        }

        DriveItemId folderItemId = DriveItemId.CreateRequired(request.FolderIdOrNull);
        DriveFileMetadata? metadataOrNull = await mDriveService.GetFileOrNullAsync(folderItemId, cancellationToken);
        if (metadataOrNull == null)
        {
            addItem(items, request.Area, request.Name, EGoogleHealthStatus.Error, "연결된 계정에서 폴더를 찾을 수 없습니다.");
            return;
        }

        if (request.FolderCheckKind == EGoogleHealthFolderCheckKind.RootFolder ||
            string.IsNullOrWhiteSpace(request.OfficialRootFolderId))
        {
            addItem(items, request.Area, request.Name, EGoogleHealthStatus.Healthy, metadataOrNull.Name + " 폴더에 접근할 수 있습니다.");
            return;
        }

        await addFolderAncestryItemAsync(items, request, folderItemId, metadataOrNull, cancellationToken);
    }

    private async Task addFolderAncestryItemAsync(
        List<GoogleHealthCheckItem> items,
        GoogleFolderHealthCheckRequest request,
        DriveItemId folderItemId,
        DriveFileMetadata metadata,
        CancellationToken cancellationToken)
    {
        DriveFolderId officialRootId = DriveFolderId.CreateRequired(request.OfficialRootFolderId);
        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(folderItemId, cancellationToken);
        bool isInsideOfficialRoot = ancestorFolderIds.Contains(officialRootId);
        addItem(
            items,
            request.Area,
            request.Name,
            isInsideOfficialRoot ? EGoogleHealthStatus.Healthy : EGoogleHealthStatus.Error,
            isInsideOfficialRoot ? metadata.Name + " 폴더가 기본 자료 위치 안에 있습니다." : metadata.Name + " 폴더가 기본 자료 위치 안에 있지 않습니다.");
    }

    private GoogleFolderHealthCheckRequest createFolderHealthCheckRequest(
        string area,
        string name,
        string? folderIdOrNull,
        string officialRootFolderId,
        EGoogleHealthFolderCheckKind folderCheckKind)
    {
        GoogleFolderHealthCheckRequest request = new GoogleFolderHealthCheckRequest();
        request.Area = area;
        request.Name = name;
        request.FolderIdOrNull = folderIdOrNull;
        request.OfficialRootFolderId = officialRootFolderId;
        request.FolderCheckKind = folderCheckKind;
        return request;
    }

    private sealed class GoogleFolderHealthCheckRequest
    {
        public string Area { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? FolderIdOrNull
        {
            get; set;
        }

        public string OfficialRootFolderId { get; set; } = string.Empty;

        public EGoogleHealthFolderCheckKind FolderCheckKind
        {
            get; set;
        }
    }
}
