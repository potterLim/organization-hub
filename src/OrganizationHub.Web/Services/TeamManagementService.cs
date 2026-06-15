using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class TeamManagementService : ITeamManagementService
{
    private const int SORT_ORDER_STEP = 10;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    public TeamManagementService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<Team>> GetTeamsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await dbContext.Teams
                .Include(team => team.UserAccounts)
                .OrderBy(team => team.SortOrder)
                .ThenBy(team => team.DisplayName)
                .ToListAsync(cancellationToken);
            return teams;
        }
    }

    public async Task<GlobalSettingFormModel> GetGlobalSettingAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            return createGlobalSettingFormModel(setting);
        }
    }

    public async Task<ServiceResult> CreateTeamAsync(TeamFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult validationResult = validateTeamForm(formModel);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            OrganizationTeamKey teamKey = formModel.TeamKeyValue;
            string requiredTeamKey = teamKey.GetRequiredValue();
            bool hasDuplicateTeamKey = await dbContext.Teams.AnyAsync(team => team.TeamKey == requiredTeamKey, cancellationToken);
            if (hasDuplicateTeamKey)
            {
                return ServiceResult.Fail("이미 사용 중인 팀 키입니다.");
            }

            Team team = new Team();
            applyTeamForm(team, formModel);
            dbContext.Teams.Add(team);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> UpdateTeamAsync(TeamFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult validationResult = validateTeamForm(formModel);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            OrganizationTeamId teamId = formModel.TeamIdValue;
            int requiredTeamId = teamId.GetRequiredValue();
            Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.Id == requiredTeamId, cancellationToken);
            if (teamOrNull == null)
            {
                return ServiceResult.Fail("팀을 찾을 수 없습니다.");
            }

            OrganizationTeamKey teamKey = formModel.TeamKeyValue;
            string requiredTeamKey = teamKey.GetRequiredValue();
            bool hasDuplicateTeamKey = await dbContext.Teams.AnyAsync(team => team.Id != requiredTeamId && team.TeamKey == requiredTeamKey, cancellationToken);
            if (hasDuplicateTeamKey)
            {
                return ServiceResult.Fail("이미 사용 중인 팀 키입니다.");
            }

            applyTeamForm(teamOrNull, formModel);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> UpdateTeamOrderAsync(OrganizationTeamOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedTeamIds.Count == 0)
        {
            return ServiceResult.Succeed();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<int> orderedTeamIdValues = request.OrderedTeamIds.Select(teamId => teamId.GetRequiredValue()).Distinct().ToList();
            List<Team> teams = await dbContext.Teams.Where(team => orderedTeamIdValues.Contains(team.Id)).ToListAsync(cancellationToken);
            if (teams.Count != orderedTeamIdValues.Count)
            {
                return ServiceResult.Fail("정렬할 팀을 찾을 수 없습니다.");
            }

            Dictionary<int, Team> teamsById = teams.ToDictionary(team => team.Id);
            int sortOrder = SORT_ORDER_STEP;
            foreach (OrganizationTeamId teamId in request.OrderedTeamIds)
            {
                Team team = teamsById[teamId.GetRequiredValue()];
                team.SortOrder = sortOrder;
                sortOrder += SORT_ORDER_STEP;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> ActivateTeamAsync(OrganizationTeamId teamId, CancellationToken cancellationToken)
    {
        return await setTeamActivationStateAsync(teamId, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivateTeamAsync(OrganizationTeamId teamId, CancellationToken cancellationToken)
    {
        return await setTeamActivationStateAsync(teamId, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setTeamActivationStateAsync(
        OrganizationTeamId teamId,
        EActivationState activationState,
        CancellationToken cancellationToken
    )
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredTeamId = teamId.GetRequiredValue();
            Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.Id == requiredTeamId, cancellationToken);
            if (teamOrNull == null)
            {
                return ServiceResult.Fail("팀을 찾을 수 없습니다.");
            }

            teamOrNull.IsActive = activationState == EActivationState.Active;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
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
            setting.ShowTodoAuthor = formModel.ShowTodoAuthor;
            setting.ShowCalendarAuthor = formModel.ShowCalendarAuthor;
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

    private ServiceResult validateTeamForm(TeamFormModel formModel)
    {
        OrganizationTeamKey teamKey = formModel.TeamKeyValue;
        if (teamKey.HasValue == false)
        {
            return ServiceResult.Fail("팀 키를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.DisplayName))
        {
            return ServiceResult.Fail("팀 표시 이름을 입력해 주세요.");
        }

        return ServiceResult.Succeed();
    }

    private void applyTeamForm(Team team, TeamFormModel formModel)
    {
        team.TeamKey = formModel.TeamKeyValue.GetRequiredValue();
        team.DisplayName = formModel.DisplayName.Trim();
        team.IsActive = formModel.IsActive;
        team.SortOrder = formModel.SortOrder;
        team.MaterialsFolderId = getTrimmedTextOrNullIfBlank(formModel.MaterialsFolderIdValue.GetOptionalValue());
        team.ApprovedFolderId = getTrimmedTextOrNullIfBlank(formModel.ApprovedFolderIdValue.GetOptionalValue());
        team.CalendarId = getTrimmedTextOrNullIfBlank(formModel.CalendarIdValue.GetOptionalValue());
        team.TaskListId = getTrimmedTextOrNullIfBlank(formModel.TaskListIdValue.GetOptionalValue());
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
        formModel.ShowTodoAuthor = setting.ShowTodoAuthor;
        formModel.ShowCalendarAuthor = setting.ShowCalendarAuthor;
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
