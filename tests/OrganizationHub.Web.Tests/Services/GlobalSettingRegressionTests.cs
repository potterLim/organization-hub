using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class GlobalSettingRegressionTests
{
    [Fact]
    public async Task OverlappingServiceSavesReturnConflictAndPreserveWinnerAsync()
    {
        SettingSaveBarrier barrier = new SettingSaveBarrier();
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE, new TestDbContextFactory(barrier)))
        {
            GlobalSettingService service = new GlobalSettingService(factory, new OrganizationPermissionService(factory));
            GlobalSettingFormModel first = await service.GetGlobalSettingAsync(CancellationToken.None);
            GlobalSettingFormModel second = await service.GetGlobalSettingAsync(CancellationToken.None);
            first.ShouldShowTodoAuthor = false;
            second.ShouldShowCalendarAuthor = false;
            Task<ServiceResult> pausedSave = service.UpdateDisplayOptionsAsync(OperationPermissionRegressionTests.createSession(), first, CancellationToken.None);
            await barrier.Paused.WaitAsync(System.TimeSpan.FromSeconds(10));
            try
            {
                Assert.True((await service.UpdateDisplayOptionsAsync(OperationPermissionRegressionTests.createSession(), second, CancellationToken.None)).IsSuccess);
            }
            finally
            {
                barrier.Release();
            }

            ServiceResult conflict = await pausedSave;
            Assert.False(conflict.IsSuccess);
            Assert.Contains("다른 관리자", conflict.ErrorMessage);
            GlobalSettingFormModel actual = await service.GetGlobalSettingAsync(CancellationToken.None);
            Assert.True(actual.ShouldShowTodoAuthor);
            Assert.False(actual.ShouldShowCalendarAuthor);
        }
    }

    [Fact]
    public async Task ConcurrentDatabaseWritersCannotSilentlyOverwriteSettingsAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        using (OrganizationHubDbContext first = factory.CreateDbContext())
        using (OrganizationHubDbContext second = factory.CreateDbContext())
        {
            GlobalSetting firstSetting = await first.GlobalSettings.SingleAsync();
            GlobalSetting secondSetting = await second.GlobalSettings.SingleAsync();
            firstSetting.OrganizationCalendarId = "new-calendar";
            secondSetting.OrganizationCalendarId = "stale-calendar";
            await first.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            using (OrganizationHubDbContext actual = factory.CreateDbContext())
            {
                Assert.Equal("new-calendar", actual.GlobalSettings.Single().OrganizationCalendarId);
            }
        }
    }

    [Fact]
    public async Task FileAssignmentPreservesDisplayAndOtherResourcesAndRejectsStaleFileChoicesAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        {
            GlobalSettingService service = new GlobalSettingService(factory, new OrganizationPermissionService(factory));
            GlobalSettingFormModel initial = await service.GetGlobalSettingAsync(CancellationToken.None);
            initial.ShouldShowTodoAuthor = false;
            Assert.True((await service.UpdateDisplayOptionsAsync(OperationPermissionRegressionTests.createSession(), initial, CancellationToken.None)).IsSuccess);
            SettingFileAssignmentCommand first = new SettingFileAssignmentCommand
            {
                Target = ESettingFileAssignmentTarget.NoticeFile,
                FileId = DriveItemId.CreateRequired("notice"),
                ExpectedRevision = initial.FileResourcesRevision
            };
            Assert.True((await service.AssignFileAsync(OperationPermissionRegressionTests.createSession(), first, CancellationToken.None)).IsSuccess);
            SettingFileAssignmentCommand stale = new SettingFileAssignmentCommand
            {
                Target = ESettingFileAssignmentTarget.NoticeFile,
                FileId = DriveItemId.CreateRequired("stale-notice"),
                ExpectedRevision = initial.FileResourcesRevision
            };
            Assert.False((await service.AssignFileAsync(OperationPermissionRegressionTests.createSession(), stale, CancellationToken.None)).IsSuccess);
            GlobalSettingFormModel actual = await service.GetGlobalSettingAsync(CancellationToken.None);
            Assert.Equal("notice", actual.FileResources.NoticeFileId);
            Assert.Equal("calendar", actual.CalendarResources.OrganizationCalendarId);
            Assert.False(actual.ShouldShowTodoAuthor);
        }
    }

    [Fact]
    public async Task DisplaySavePreservesConcurrentResourceAssignmentsAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        {
            GlobalSettingService service = new GlobalSettingService(factory, new OrganizationPermissionService(factory));
            GlobalSettingFormModel staleForm = await service.GetGlobalSettingAsync(CancellationToken.None);
            using (OrganizationHubDbContext db = factory.CreateDbContext())
            {
                db.GlobalSettings.Single().OfficialRootFolderId = "concurrent-root";
                await db.SaveChangesAsync();
            }

            staleForm.ShouldShowTodoAuthor = false;
            ServiceResult result = await service.UpdateDisplayOptionsAsync(OperationPermissionRegressionTests.createSession(), staleForm, CancellationToken.None);
            Assert.True(result.IsSuccess);
            GlobalSettingFormModel actual = await service.GetGlobalSettingAsync(CancellationToken.None);
            Assert.Equal("concurrent-root", actual.DriveResources.OfficialRootFolderId);
            Assert.False(actual.ShouldShowTodoAuthor);
        }
    }

    [Fact]
    public async Task StaleDisplayOptionsAreRejectedWithoutOverwritingNewerOptionsAsync()
    {
        using (TestDbContextFactory factory = OperationPermissionRegressionTests.createFactory(PermissionKeys.SYSTEM_SETTINGS_MANAGE))
        {
            GlobalSettingService service = new GlobalSettingService(factory, new OrganizationPermissionService(factory));
            GlobalSettingFormModel first = await service.GetGlobalSettingAsync(CancellationToken.None);
            GlobalSettingFormModel stale = await service.GetGlobalSettingAsync(CancellationToken.None);
            first.ShouldShowTodoAuthor = false;
            Assert.True((await service.UpdateDisplayOptionsAsync(OperationPermissionRegressionTests.createSession(), first, CancellationToken.None)).IsSuccess);
            stale.ShouldShowCalendarAuthor = false;
            Assert.False((await service.UpdateDisplayOptionsAsync(OperationPermissionRegressionTests.createSession(), stale, CancellationToken.None)).IsSuccess);
            GlobalSettingFormModel actual = await service.GetGlobalSettingAsync(CancellationToken.None);
            Assert.False(actual.ShouldShowTodoAuthor);
            Assert.True(actual.ShouldShowCalendarAuthor);
        }
    }
}
