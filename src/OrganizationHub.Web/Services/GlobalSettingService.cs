using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class GlobalSettingService : IGlobalSettingService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public GlobalSettingService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IOrganizationPermissionService organizationPermissionService)
    {
        mDbContextFactory = dbContextFactory;
        mOrganizationPermissionService = organizationPermissionService;
    }

    public async Task<GlobalSettingFormModel> GetGlobalSettingAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            return createGlobalSettingFormModel(setting);
        }
    }

    public async Task<ServiceResult> UpdateDisplayOptionsAsync(UserSession userSession, GlobalSettingFormModel formModel, CancellationToken cancellationToken)
    {
        if (await canManageSettingsAsync(userSession, cancellationToken) == false)
        {
            return ServiceResult.Fail("설정을 변경할 권한이 없습니다. 현재 권한을 확인해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            if (setting.DisplayOptionsRevision != formModel.DisplayOptionsRevision)
            {
                return createConflictResult();
            }

            setting.ShouldShowTodoAuthor = formModel.ShouldShowTodoAuthor;
            setting.ShouldShowCalendarAuthor = formModel.ShouldShowCalendarAuthor;
            return await saveSettingsAsync(dbContext, cancellationToken);
        }
    }

    public async Task<ServiceResult> AssignFileAsync(UserSession userSession, SettingFileAssignmentCommand command, CancellationToken cancellationToken)
    {
        if (await canManageSettingsAsync(userSession, cancellationToken) == false)
        {
            return ServiceResult.Fail("설정을 변경할 권한이 없습니다. 현재 권한을 확인해 주세요.");
        }

        if (command.FileId.HasValue == false)
        {
            return ServiceResult.Fail("연결할 파일을 선택해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            if (setting.FileResourcesRevision != command.ExpectedRevision)
            {
                return createConflictResult();
            }

            switch (command.Target)
            {
                case ESettingFileAssignmentTarget.NoticeFile:
                    setting.NoticeFileId = command.FileId.GetRequiredValue();
                    break;
                case ESettingFileAssignmentTarget.WorkGuidelineFile:
                    setting.WorkGuidelineFileId = command.FileId.GetRequiredValue();
                    break;
                default:
                    return ServiceResult.Fail("연결할 파일 대상을 확인해 주세요.");
            }

            return await saveSettingsAsync(dbContext, cancellationToken);
        }
    }

    private Task<bool> canManageSettingsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        return mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(PermissionKeys.SYSTEM_SETTINGS_MANAGE), null, cancellationToken);
    }

    private static async Task<ServiceResult> saveSettingsAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
        catch (DbUpdateConcurrencyException)
        {
            return createConflictResult();
        }
    }

    private static ServiceResult createConflictResult()
    {
        return ServiceResult.Fail("다른 관리자가 설정을 변경했습니다. 최신 설정을 불러온 뒤 다시 적용해 주세요.");
    }

    private async Task<GlobalSetting> getGlobalSettingAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
        if (settingOrNull != null)
        {
            return settingOrNull;
        }

        GlobalSetting setting = new GlobalSetting();
        dbContext.GlobalSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private GlobalSettingFormModel createGlobalSettingFormModel(GlobalSetting setting)
    {
        GlobalSettingFormModel formModel = new GlobalSettingFormModel();
        formModel.Id = setting.Id;
        formModel.DisplayOptionsRevision = setting.DisplayOptionsRevision;
        formModel.FileResourcesRevision = setting.FileResourcesRevision;
        formModel.DriveResources.OfficialRootFolderId = getTextOrEmpty(setting.OfficialRootFolderId);
        formModel.DriveResources.PublicFolderId = getTextOrEmpty(setting.PublicFolderId);
        formModel.DriveResources.CommonFolderId = getTextOrEmpty(setting.CommonFolderId);
        formModel.DriveResources.ResponsibleResourcesFolderId = getTextOrEmpty(setting.ResponsibleResourcesFolderId);
        formModel.DriveResources.WorkResourcesFolderId = getTextOrEmpty(setting.WorkResourcesFolderId);
        formModel.CalendarResources.OrganizationCalendarId = getTextOrEmpty(setting.OrganizationCalendarId);
        formModel.CalendarResources.ResponsibleCalendarId = getTextOrEmpty(setting.ResponsibleCalendarId);
        formModel.TaskResources.OrganizationTaskListId = getTextOrEmpty(setting.OrganizationTaskListId);
        formModel.TaskResources.ResponsibleTaskListId = getTextOrEmpty(setting.ResponsibleTaskListId);
        formModel.FileResources.NoticeFileId = getTextOrEmpty(setting.NoticeFileId);
        formModel.FileResources.WorkGuidelineFileId = getTextOrEmpty(setting.WorkGuidelineFileId);
        formModel.ShouldShowTodoAuthor = setting.ShouldShowTodoAuthor;
        formModel.ShouldShowCalendarAuthor = setting.ShouldShowCalendarAuthor;
        return formModel;
    }

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }
}
