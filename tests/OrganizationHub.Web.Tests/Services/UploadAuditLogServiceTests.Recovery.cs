using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class UploadAuditLogServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedInitialJournalPreventsRemoteFileCreationOrReplacementAsync(bool isReplacement)
    {
        using (TestDbContextFactory factory = createConfiguredDbContextFactory())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (MemoryStream content = new MemoryStream(new byte[] { 1 }))
        {
            grantPlanningUpload(factory);
            FakeDriveService drive = createConfiguredDriveService();
            drive.AddFile("previous-file", "previous.pdf", "application/pdf", "planning-materials-folder");
            PermissionService permissions = new PermissionService(factory, drive, cache, new OrganizationPermissionService(factory));
            UploadAuditLogService service = new UploadAuditLogService(factory, permissions, drive);
            DriveFolderReference folder = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
            DriveItemName name = DriveItemName.CreateRequired("must-not-exist.pdf");
            FileContentType contentType = FileContentType.CreateOptional("application/pdf");
            DriveFileUploadRequest request = isReplacement
                ? DriveFileUploadRequest.CreateReplacementFile(folder.FolderId, content, name, contentType, DriveItemId.CreateRequired("previous-file"))
                : DriveFileUploadRequest.CreateNewFile(folder.FolderId, content, name, contentType);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_pending BEFORE INSERT ON PendingUploadAudits BEGIN SELECT RAISE(FAIL,'journal unavailable'); END;");
            }
            await Assert.ThrowsAsync<UploadPreparationException>(() => service.UploadFileAsync(createPlanningUserSession(), folder, request, CancellationToken.None));
            Assert.DoesNotContain(await drive.ListFilesAsync(folder.FolderId, CancellationToken.None), item => item.Name == "must-not-exist.pdf");
            Assert.Equal("previous.pdf", (await drive.GetFileOrNullAsync(DriveItemId.CreateRequired("previous-file"), CancellationToken.None))!.Name);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.PendingUploadAudits);
                Assert.DoesNotContain(db.UploadAuditLogs, item => item.FileName == "must-not-exist.pdf");
            }
        }
    }

    [Fact]
    public async Task LaterReplacementDoesNotEraseAnEarlierConfirmedUploadReceiptAsync()
    {
        using (TestDbContextFactory factory = createConfiguredDbContextFactory())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (MemoryStream firstContent = new MemoryStream(new byte[] { 1 }))
        using (MemoryStream secondContent = new MemoryStream(new byte[] { 2 }))
        using (VersionedGoogleTransport transport = new VersionedGoogleTransport())
        {
            grantPlanningUpload(factory);
            FakeDriveService drive = createConfiguredDriveService();
            drive.AddFile("existing", "previous.pdf", "application/pdf", "planning-materials-folder");
            PermissionService permissions = new PermissionService(factory, drive, cache, new OrganizationPermissionService(factory));
            UploadAuditLogService service = new UploadAuditLogService(factory, permissions, drive);
            DriveFolderReference folder = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_receipts BEFORE INSERT ON UploadAuditLogs BEGIN SELECT RAISE(FAIL,'receipt unavailable'); END;");
            }
            DriveFileUploadRequest first = DriveFileUploadRequest.CreateReplacementFile(folder.FolderId, firstContent, DriveItemName.CreateRequired("first.pdf"), FileContentType.CreateOptional("application/pdf"), DriveItemId.CreateRequired("existing"));
            DriveFileUploadRequest second = DriveFileUploadRequest.CreateReplacementFile(folder.FolderId, secondContent, DriveItemName.CreateRequired("second.pdf"), FileContentType.CreateOptional("application/pdf"), DriveItemId.CreateRequired("existing"));
            await service.UploadFileAsync(createPlanningUserSession(), folder, first, CancellationToken.None);
            await service.UploadFileAsync(createPlanningUserSession(), folder, second, CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Equal(2, db.PendingUploadAudits.Count(item => item.IsUploadConfirmed));
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_receipts;");
            }
            await new AuditRecoveryService(factory, transport, NullLogger<AuditRecoveryService>.Instance, drive).RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.PendingUploadAudits);
                Assert.Single(db.UploadAuditLogs.Where(item => item.FileId == "existing" && item.FileName == "first.pdf"));
                Assert.Single(db.UploadAuditLogs.Where(item => item.FileId == "existing" && item.FileName == "second.pdf"));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadAndReplacementRetainDurableReceiptsWhenAuditInsertFailsAsync(bool isReplacement)
    {
        using (TestDbContextFactory factory = createConfiguredDbContextFactory())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (MemoryStream content = new MemoryStream(new byte[] { 1 }))
        using (VersionedGoogleTransport transport = new VersionedGoogleTransport())
        {
            grantPlanningUpload(factory);
            FakeDriveService drive = createConfiguredDriveService();
            DriveFolderReference folder = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
            drive.AddFile("existing-file", "previous.pdf", "application/pdf", "planning-materials-folder");
            PermissionService permissions = new PermissionService(factory, drive, cache, new OrganizationPermissionService(factory));
            UploadAuditLogService service = new UploadAuditLogService(factory, permissions, drive);
            DriveItemName name = DriveItemName.CreateRequired("tracked.pdf");
            FileContentType contentType = FileContentType.CreateOptional("application/pdf");
            DriveFileUploadRequest request = isReplacement
                ? DriveFileUploadRequest.CreateReplacementFile(folder.FolderId, content, name, contentType, DriveItemId.CreateRequired("existing-file"))
                : DriveFileUploadRequest.CreateNewFile(folder.FolderId, content, name, contentType);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_receipt BEFORE INSERT ON UploadAuditLogs BEGIN SELECT RAISE(FAIL,'receipt unavailable'); END;");
            }
            DriveFileItem uploaded = await service.UploadFileAsync(createPlanningUserSession(), folder, request, CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Single(db.PendingUploadAudits);
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_receipt;");
            }
            AuditRecoveryService recovery = new AuditRecoveryService(factory, transport, NullLogger<AuditRecoveryService>.Instance, drive);
            await recovery.RecoverAsync(CancellationToken.None);
            await recovery.RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.PendingUploadAudits);
                Assert.Single(db.UploadAuditLogs.Where(item => item.FileId == uploaded.Id));
                Assert.Equal(createPlanningUserSession().DisplayName, db.UploadAuditLogs.Single(item => item.FileId == uploaded.Id).UploaderDisplayName);
            }
            Assert.Single(await drive.ListFilesAsync(folder.FolderId, CancellationToken.None), item => item.Id == uploaded.Id);
        }
    }

    [Fact]
    public async Task LostGoogleUploadResponseRecoversAuthorWithoutRepeatingUploadAsync()
    {
        using (TestDbContextFactory factory = createConfiguredDbContextFactory())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        using (MemoryStream content = new MemoryStream(new byte[] { 1, 2, 3 }))
        using (DriveMutationTransport transport = new DriveMutationTransport())
        {
            grantPlanningUpload(factory);
            transport.HasFile = false;
            GoogleDriveService drive = new GoogleDriveService(transport, Options.Create(new GoogleApiOptions()), cache, new ChangeVersionService());
            FakeDriveService permissionDrive = createConfiguredDriveService();
            PermissionService permissions = new PermissionService(factory, permissionDrive, cache, new OrganizationPermissionService(factory));
            UploadAuditLogService service = new UploadAuditLogService(factory, permissions, drive);
            DriveFolderReference folder = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
            DriveFileUploadRequest request = DriveFileUploadRequest.CreateNewFile(folder.FolderId, content, DriveItemName.CreateRequired("response-lost.pdf"), FileContentType.CreateOptional("application/pdf"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadFileAsync(createPlanningUserSession(), folder, request, CancellationToken.None));
            AuditRecoveryService recovery = new AuditRecoveryService(factory, transport, NullLogger<AuditRecoveryService>.Instance, drive);
            await recovery.RecoverAsync(CancellationToken.None);
            await recovery.RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.PendingUploadAudits);
                Assert.Single(db.UploadAuditLogs.Where(item => item.FileId == "reserved-file"));
            }
            Assert.Equal(1, transport.Writes);
        }
    }

    private static void grantPlanningUpload(TestDbContextFactory factory)
    {
        using (OrganizationHubDbContext db = factory.CreateDbContext())
        {
            OrganizationHub.Web.Data.Entities.OrganizationPosition position = db.Positions.Include(item => item.PermissionGrants).Single(item => item.TeamId == 1);
            addPermissionGrant(position, PermissionKeys.FILES_UPLOAD, EPermissionScopeType.OwnTeam);
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task FailedAuditIsDurableAndRecoversOnceWithoutUploadingAgainAsync()
    {
        using (TestDbContextFactory factory = createConfiguredDbContextFactory())
        using (MemoryCache cache = new MemoryCache(new MemoryCacheOptions()))
        {
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_audit BEFORE INSERT ON UploadAuditLogs BEGIN SELECT RAISE(FAIL, 'audit unavailable'); END;");
            }
            FakeDriveService drive = createConfiguredDriveService();
            PermissionService permissions = new PermissionService(factory, drive, cache, new OrganizationPermissionService(factory));
            UploadAuditLogService service = new UploadAuditLogService(factory, permissions, drive);
            DriveFileItem file = new DriveFileItem();
            file.Id = "recover-file";
            file.Name = "회복.pdf";
            await Assert.ThrowsAsync<DbUpdateException>(() => service.RecordUploadAsync(createPlanningUserSession(), createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed), file, CancellationToken.None));
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Single(db.PendingUploadAudits);
                Assert.DoesNotContain(db.UploadAuditLogs, item => item.FileId == file.Id);
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_audit;");
            }
            AuditRecoveryService recovery = new AuditRecoveryService(factory, new VersionedGoogleTransport(), NullLogger<AuditRecoveryService>.Instance);
            await recovery.RecoverAsync(CancellationToken.None);
            await recovery.RecoverAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                Assert.Empty(db.PendingUploadAudits);
                Assert.Single(db.UploadAuditLogs.Where(item => item.FileId == file.Id));
                Assert.Equal(createPlanningUserSession().DisplayName, db.UploadAuditLogs.Single(item => item.FileId == file.Id).UploaderDisplayName);
            }
        }
    }
}
