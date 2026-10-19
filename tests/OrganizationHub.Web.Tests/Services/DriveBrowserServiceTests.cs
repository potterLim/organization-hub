using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class DriveBrowserServiceTests
{
    [Fact]
    public async Task GetFolderFilesMergesRecentUploadAuditWhenDriveListIsNotCaughtUpAsync()
    {
        DriveFolderReference planningFolder = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
        FakePermissionService permissionService = new FakePermissionService(planningFolder);
        FakeUploadAuditLogService uploadAuditLogService = new FakeUploadAuditLogService();
        uploadAuditLogService.RecentUploadsForFolder.Add(createRecentUpload("fresh-file", "방금올린자료.pdf"));
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            DriveBrowserService driveBrowserService = createDriveBrowserService(permissionService, uploadAuditLogService, memoryCache);
            UserSession userSession = createUserSession();

            DriveFolderBrowseRequest request = new DriveFolderBrowseRequest(userSession, EDriveFolderKind.TeamMaterials, planningFolder.FolderId, planningFolder.FolderId);
            IReadOnlyList<DriveFileItem> files = await driveBrowserService.GetFolderFilesAsync(request, CancellationToken.None);

            DriveFileItem file = Assert.Single(files);
            Assert.Equal("fresh-file", file.Id);
            Assert.Equal("방금올린자료.pdf", file.Name);
            Assert.Equal("application/pdf", file.MimeType);
            Assert.True(file.CanPreviewInSite);
            Assert.Equal("기획팀", file.TeamName);
            Assert.Equal("테스트 사용자", file.UploadedByDisplayName);
        }
    }

    [Fact]
    public async Task GetFolderFilesDoesNotMergeRecentUploadAuditWhenKeywordDoesNotMatchAsync()
    {
        DriveFolderReference planningFolder = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
        FakePermissionService permissionService = new FakePermissionService(planningFolder);
        FakeUploadAuditLogService uploadAuditLogService = new FakeUploadAuditLogService();
        uploadAuditLogService.RecentUploadsForFolder.Add(createRecentUpload("fresh-file", "방금올린자료.pdf"));
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            DriveBrowserService driveBrowserService = createDriveBrowserService(permissionService, uploadAuditLogService, memoryCache);
            UserSession userSession = createUserSession();

            DriveFolderBrowseRequest request = new DriveFolderBrowseRequest(userSession, EDriveFolderKind.TeamMaterials, planningFolder.FolderId, planningFolder.FolderId);
            request.Keyword = SearchKeyword.CreateOptional("다른자료");

            IReadOnlyList<DriveFileItem> files = await driveBrowserService.GetFolderFilesAsync(request, CancellationToken.None);

            Assert.Empty(files);
        }
    }

    [Fact]
    public async Task GetFilesUsesFolderKindScopedPermissionLookupAsync()
    {
        DriveFolderReference planningMaterialsFolder = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
        DriveFolderReference commonFolder = createGlobalFolderReference("common-folder", "공통 자료", EDriveFolderKind.Common, EDriveFolderUploadAccess.UploadAllowed);
        FakePermissionService permissionService = new FakePermissionService(planningMaterialsFolder, commonFolder);
        FakeUploadAuditLogService uploadAuditLogService = new FakeUploadAuditLogService();
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            DriveBrowserService driveBrowserService = createDriveBrowserService(permissionService, uploadAuditLogService, memoryCache);
            UserSession userSession = createUserSession();

            await driveBrowserService.GetFilesAsync(userSession, EDriveFolderKind.TeamMaterials, SearchKeyword.Empty, CancellationToken.None);

            Assert.Equal(0, permissionService.AllReadableFolderCallCount);
            EDriveFolderKind requestedFolderKind = Assert.Single(permissionService.KindScopedReadableFolderCallKinds);
            Assert.Equal(EDriveFolderKind.TeamMaterials, requestedFolderKind);
        }
    }

    private static UserSession createUserSession()
    {
        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(1),
                Username = Username.CreateOptional("planning.member"),
                DisplayName = DisplayName.CreateOptional("테스트 사용자"),
                StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser,
                OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                TeamKey = new OrganizationTeamKey("planning"),
                TeamDisplayName = DisplayName.CreateOptional("기획팀")
            });
    }

    private static DriveFolderReference createTeamFolderReference(string folderId, string displayName, int teamId, string teamName, EDriveFolderUploadAccess uploadAccess)
    {
        DriveFolderOwnerContext ownerContext = new DriveFolderOwnerContext(OrganizationTeamId.CreateExisting(teamId), OrganizationTeamKey.Empty, teamName);
        DriveFolderReferenceCreationRequest request = new DriveFolderReferenceCreationRequest(DriveFolderId.CreateOptional(folderId), displayName, EDriveFolderKind.TeamMaterials, uploadAccess, ownerContext);
        return new DriveFolderReference(request);
    }

    private static DriveFolderReference createGlobalFolderReference(string folderId, string displayName, EDriveFolderKind folderKind, EDriveFolderUploadAccess uploadAccess)
    {
        DriveFolderReferenceCreationRequest request = new DriveFolderReferenceCreationRequest(DriveFolderId.CreateOptional(folderId), displayName, folderKind, uploadAccess);
        return new DriveFolderReference(request);
    }

    private static UploadAuditLogItem createRecentUpload(string fileId, string fileName)
    {
        UploadAuditLogItem item = new UploadAuditLogItem();
        item.FileId = fileId;
        item.FileName = fileName;
        item.FolderDisplayName = "기획팀 자료";
        item.FolderKind = EDriveFolderKind.TeamMaterials;
        item.UploaderDisplayName = "테스트 사용자";
        item.TeamName = "기획팀";
        item.UploadedAt = DateTime.UtcNow;
        return item;
    }

    private static DriveBrowserService createDriveBrowserService(
        FakePermissionService permissionService,
        FakeUploadAuditLogService uploadAuditLogService,
        IMemoryCache memoryCache)
    {
        return new DriveBrowserService(
            permissionService,
            new FakeDriveService(),
            uploadAuditLogService,
            memoryCache,
            new ChangeVersionService(),
            new FakeRecentUploadedFileCacheRefreshQueue());
    }

    private sealed class FakePermissionService : IPermissionService
    {
        private readonly IReadOnlyList<DriveFolderReference> mFolders;
        public int AllReadableFolderCallCount
        {
            get; private set;
        }
        public List<EDriveFolderKind> KindScopedReadableFolderCallKinds { get; } = new List<EDriveFolderKind>();

        public FakePermissionService(params DriveFolderReference[] folders)
        {
            mFolders = folders;
        }

        public Task<IReadOnlyList<DriveFolderReference>> GetReadableDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken)
        {
            AllReadableFolderCallCount++;
            return Task.FromResult(mFolders);
        }

        public Task<IReadOnlyList<DriveFolderReference>> GetReadableDriveFoldersAsync(UserSession userSession, EDriveFolderKind folderKind, CancellationToken cancellationToken)
        {
            KindScopedReadableFolderCallKinds.Add(folderKind);
            IReadOnlyList<DriveFolderReference> folders = mFolders.Where(folder => folder.FolderKind == folderKind).ToList();
            return Task.FromResult(folders);
        }

        public Task<IReadOnlyList<DriveFolderReference>> GetUploadableDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken)
        {
            return Task.FromResult(mFolders);
        }

        public Task<IReadOnlyList<DriveFolderReference>> GetApprovalDestinationDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken)
        {
            IReadOnlyList<DriveFolderReference> folders = new List<DriveFolderReference>();
            return Task.FromResult(folders);
        }

        public Task<IReadOnlyList<CalendarReference>> GetReadableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
        {
            IReadOnlyList<CalendarReference> calendars = new List<CalendarReference>();
            return Task.FromResult(calendars);
        }

        public Task<IReadOnlyList<CalendarReference>> GetWritableCalendarsAsync(UserSession userSession, EResourceOperation operation, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CalendarReference>>(new List<CalendarReference>());
        }

        public Task<bool> CanWriteCalendarAsync(UserSession userSession, CalendarId calendarId, EResourceOperation operation, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<CalendarReference>> GetWritableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
        {
            IReadOnlyList<CalendarReference> calendars = new List<CalendarReference>();
            return Task.FromResult(calendars);
        }

        public Task<bool> CanReadFileAsync(UserSession userSession, DriveItemId fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideReadFileAsync(UserSession userSession, DriveItemId fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(createDeniedPermissionDecision());
        }

        public Task<bool> CanUploadToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideUploadToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(createDeniedPermissionDecision());
        }

        public Task<bool> CanCreateFolderInFolderAsync(UserSession userSession, DriveFolderId parentFolderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideCreateFolderInFolderAsync(UserSession userSession, DriveFolderId parentFolderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(createDeniedPermissionDecision());
        }

        public Task<bool> CanDeleteInFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideDeleteInFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(createDeniedPermissionDecision());
        }

        public Task<bool> CanStoreApprovedFileToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideStoreApprovedFileToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(createDeniedPermissionDecision());
        }

        public Task<bool> CanReadCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideReadCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
        {
            return Task.FromResult(createDeniedPermissionDecision());
        }

        public Task<bool> CanWriteCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideWriteCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
        {
            return Task.FromResult(createDeniedPermissionDecision());
        }

        private static PermissionDecision createDeniedPermissionDecision()
        {
            return PermissionDecision.Deny(
                PermissionDecisionReason.CreateRequired("테스트 권한 없음"),
                PermissionScopeLabel.CreateRequired("테스트"));
        }
    }

    private sealed class FakeUploadAuditLogService : IUploadAuditLogService
    {
        public List<UploadAuditLogItem> RecentUploadsForFolder { get; } = new List<UploadAuditLogItem>();

        public Task RecordUploadAsync(UserSession userSession, DriveFolderReference targetFolder, DriveFileItem uploadedFile, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AttachUploadMetadataAsync(IReadOnlyList<DriveFileItem> files, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsAsync(UserSession userSession, RecentItemLimit maximumCount, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForReadableRootsAsync(UserSession userSession, EDriveFolderKind folderKind, RecentItemLimit maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadSnapshotsForReadableRootsAsync(UserSession userSession, EDriveFolderKind folderKind, RecentItemLimit maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForRootAsync(UserSession userSession, DriveFolderReference rootFolder, RecentItemLimit maximumCount, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForFolderAsync(DriveFolderId folderId, TimeSpan maximumAge, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = RecentUploadsForFolder;
            return Task.FromResult(items);
        }
    }

    private sealed class FakeRecentUploadedFileCacheRefreshQueue : IRecentUploadedFileCacheRefreshQueue
    {
        public bool TryEnqueue(RecentUploadedFileCacheRefreshRequest request)
        {
            return true;
        }
    }
}
