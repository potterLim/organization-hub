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
        DriveFolderReference planningFolder = new DriveFolderReference("planning-materials-folder", "기획팀 자료", EDriveFolderKind.TeamMaterials, 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
        FakePermissionService permissionService = new FakePermissionService(planningFolder);
        FakeUploadAuditLogService uploadAuditLogService = new FakeUploadAuditLogService();
        uploadAuditLogService.RecentUploadsForFolder.Add(createRecentUpload("fresh-file", "방금올린자료.pdf"));
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            DriveBrowserService driveBrowserService = new DriveBrowserService(permissionService, new FakeDriveService(), uploadAuditLogService, memoryCache, new ChangeVersionService());
            UserSession userSession = createUserSession();

            IReadOnlyList<DriveFileItem> files = await driveBrowserService.GetFolderFilesAsync(userSession, EDriveFolderKind.TeamMaterials, planningFolder.FolderID, planningFolder.FolderID, string.Empty, CancellationToken.None);

            DriveFileItem file = Assert.Single(files);
            Assert.Equal("fresh-file", file.ID);
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
        DriveFolderReference planningFolder = new DriveFolderReference("planning-materials-folder", "기획팀 자료", EDriveFolderKind.TeamMaterials, 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
        FakePermissionService permissionService = new FakePermissionService(planningFolder);
        FakeUploadAuditLogService uploadAuditLogService = new FakeUploadAuditLogService();
        uploadAuditLogService.RecentUploadsForFolder.Add(createRecentUpload("fresh-file", "방금올린자료.pdf"));
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            DriveBrowserService driveBrowserService = new DriveBrowserService(permissionService, new FakeDriveService(), uploadAuditLogService, memoryCache, new ChangeVersionService());
            UserSession userSession = createUserSession();

            IReadOnlyList<DriveFileItem> files = await driveBrowserService.GetFolderFilesAsync(userSession, EDriveFolderKind.TeamMaterials, planningFolder.FolderID, planningFolder.FolderID, "다른자료", CancellationToken.None);

            Assert.Empty(files);
        }
    }

    [Fact]
    public async Task GetFilesUsesFolderKindScopedPermissionLookupAsync()
    {
        DriveFolderReference planningMaterialsFolder = new DriveFolderReference("planning-materials-folder", "기획팀 자료", EDriveFolderKind.TeamMaterials, 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
        DriveFolderReference commonFolder = new DriveFolderReference("common-folder", "공통 자료", EDriveFolderKind.Common, null, string.Empty, EDriveFolderUploadAccess.UploadAllowed);
        FakePermissionService permissionService = new FakePermissionService(planningMaterialsFolder, commonFolder);
        FakeUploadAuditLogService uploadAuditLogService = new FakeUploadAuditLogService();
        using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
        {
            DriveBrowserService driveBrowserService = new DriveBrowserService(permissionService, new FakeDriveService(), uploadAuditLogService, memoryCache, new ChangeVersionService());
            UserSession userSession = createUserSession();

            await driveBrowserService.GetFilesAsync(userSession, EDriveFolderKind.TeamMaterials, string.Empty, CancellationToken.None);

            Assert.Equal(0, permissionService.AllReadableFolderCallCount);
            EDriveFolderKind requestedFolderKind = Assert.Single(permissionService.KindScopedReadableFolderCallKinds);
            Assert.Equal(EDriveFolderKind.TeamMaterials, requestedFolderKind);
        }
    }

    private static UserSession createUserSession()
    {
        return new UserSession(1, "planning.member", "테스트 사용자", EUserRole.TeamUser, 1, "planning", "기획팀");
    }

    private static UploadAuditLogItem createRecentUpload(string fileID, string fileName)
    {
        UploadAuditLogItem item = new UploadAuditLogItem();
        item.FileID = fileID;
        item.FileName = fileName;
        item.FolderDisplayName = "기획팀 자료";
        item.FolderKind = EDriveFolderKind.TeamMaterials;
        item.UploaderDisplayName = "테스트 사용자";
        item.TeamName = "기획팀";
        item.UploadedAt = DateTime.UtcNow;
        return item;
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

        public Task<IReadOnlyList<CalendarReference>> GetWritableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
        {
            IReadOnlyList<CalendarReference> calendars = new List<CalendarReference>();
            return Task.FromResult(calendars);
        }

        public Task<bool> CanReadFileAsync(UserSession userSession, string fileID, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideReadFileAsync(UserSession userSession, string fileID, CancellationToken cancellationToken)
        {
            return Task.FromResult(PermissionDecision.Deny("테스트 권한 없음", "테스트"));
        }

        public Task<bool> CanUploadToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideUploadToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(PermissionDecision.Deny("테스트 권한 없음", "테스트"));
        }

        public Task<bool> CanCreateFolderInFolderAsync(UserSession userSession, string parentFolderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideCreateFolderInFolderAsync(UserSession userSession, string parentFolderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(PermissionDecision.Deny("테스트 권한 없음", "테스트"));
        }

        public Task<bool> CanDeleteInFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideDeleteInFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(PermissionDecision.Deny("테스트 권한 없음", "테스트"));
        }

        public bool CanDeleteInsideRoot(UserSession userSession, DriveFolderReference rootFolder)
        {
            return false;
        }

        public PermissionDecision DecideDeleteInsideRoot(UserSession userSession, DriveFolderReference rootFolder)
        {
            return PermissionDecision.Deny("테스트 권한 없음", "테스트");
        }

        public Task<bool> CanStoreApprovedFileToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideStoreApprovedFileToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
        {
            return Task.FromResult(PermissionDecision.Deny("테스트 권한 없음", "테스트"));
        }

        public Task<bool> CanReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
        {
            return Task.FromResult(PermissionDecision.Deny("테스트 권한 없음", "테스트"));
        }

        public Task<bool> CanWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<PermissionDecision> DecideWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
        {
            return Task.FromResult(PermissionDecision.Deny("테스트 권한 없음", "테스트"));
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

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsAsync(UserSession userSession, int maximumCount, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForReadableRootsAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForRootAsync(UserSession userSession, DriveFolderReference rootFolder, int maximumCount, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForFolderAsync(string folderID, TimeSpan maximumAge, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadAuditLogItem> items = RecentUploadsForFolder;
            return Task.FromResult(items);
        }
    }
}
