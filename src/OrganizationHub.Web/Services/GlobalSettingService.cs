using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class GlobalSettingService : IGlobalSettingService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    public GlobalSettingService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<GlobalSettingFormModel> GetGlobalSettingAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            return createGlobalSettingFormModel(setting);
        }
    }

    public async Task<ServiceResult> UpdateGlobalSettingAsync(GlobalSettingFormModel formModel, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            applyGlobalDriveResources(setting, formModel.DriveResources);
            applyGlobalCalendarResources(setting, formModel.CalendarResources);
            applyGlobalTaskResources(setting, formModel.TaskResources);
            applyGlobalFileResources(setting, formModel.FileResources);
            setting.ShouldShowTodoAuthor = formModel.ShouldShowTodoAuthor;
            setting.ShouldShowCalendarAuthor = formModel.ShouldShowCalendarAuthor;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
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

    private void applyGlobalDriveResources(GlobalSetting setting, GlobalSettingDriveResourceFormModel driveResources)
    {
        setting.OfficialRootFolderId = getTrimmedTextOrNullIfBlank(driveResources.OfficialRootFolderIdValue.GetOptionalValue());
        setting.PublicFolderId = getTrimmedTextOrNullIfBlank(driveResources.PublicFolderIdValue.GetOptionalValue());
        setting.CommonFolderId = getTrimmedTextOrNullIfBlank(driveResources.CommonFolderIdValue.GetOptionalValue());
        setting.ResponsibleResourcesFolderId = getTrimmedTextOrNullIfBlank(driveResources.ResponsibleResourcesFolderIdValue.GetOptionalValue());
        setting.WorkResourcesFolderId = getTrimmedTextOrNullIfBlank(driveResources.WorkResourcesFolderIdValue.GetOptionalValue());
    }

    private void applyGlobalCalendarResources(GlobalSetting setting, GlobalSettingCalendarResourceFormModel calendarResources)
    {
        setting.OrganizationCalendarId = getTrimmedTextOrNullIfBlank(calendarResources.OrganizationCalendarIdValue.GetOptionalValue());
        setting.ResponsibleCalendarId = getTrimmedTextOrNullIfBlank(calendarResources.ResponsibleCalendarIdValue.GetOptionalValue());
    }

    private void applyGlobalTaskResources(GlobalSetting setting, GlobalSettingTaskResourceFormModel taskResources)
    {
        setting.OrganizationTaskListId = getTrimmedTextOrNullIfBlank(taskResources.OrganizationTaskListIdValue.GetOptionalValue());
        setting.ResponsibleTaskListId = getTrimmedTextOrNullIfBlank(taskResources.ResponsibleTaskListIdValue.GetOptionalValue());
    }

    private void applyGlobalFileResources(GlobalSetting setting, GlobalSettingFileResourceFormModel fileResources)
    {
        setting.NoticeFileId = getTrimmedTextOrNullIfBlank(fileResources.NoticeFileIdValue.GetOptionalValue());
        setting.WorkGuidelineFileId = getTrimmedTextOrNullIfBlank(fileResources.WorkGuidelineFileIdValue.GetOptionalValue());
    }

    private string? getTrimmedTextOrNullIfBlank(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
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
