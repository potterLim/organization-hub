using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
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
