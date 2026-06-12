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
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;

    public TeamManagementService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
    {
        mDbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<Team>> GetTeamsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await dbContext.Teams.Include(team => team.UserAccounts).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToListAsync(cancellationToken);
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
            string teamKey = formModel.TeamKey.Trim();
            bool hasDuplicateTeamKey = await dbContext.Teams.AnyAsync(team => team.TeamKey == teamKey, cancellationToken);
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
            Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == formModel.ID, cancellationToken);
            if (teamOrNull == null)
            {
                return ServiceResult.Fail("팀을 찾을 수 없습니다.");
            }

            string teamKey = formModel.TeamKey.Trim();
            bool hasDuplicateTeamKey = await dbContext.Teams.AnyAsync(team => team.ID != formModel.ID && team.TeamKey == teamKey, cancellationToken);
            if (hasDuplicateTeamKey)
            {
                return ServiceResult.Fail("이미 사용 중인 팀 키입니다.");
            }

            applyTeamForm(teamOrNull, formModel);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> ActivateTeamAsync(int teamID, CancellationToken cancellationToken)
    {
        return await setTeamActivationStateAsync(teamID, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivateTeamAsync(int teamID, CancellationToken cancellationToken)
    {
        return await setTeamActivationStateAsync(teamID, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setTeamActivationStateAsync(int teamID, EActivationState activationState, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == teamID, cancellationToken);
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
            setting.OfficialRootFolderID = getEmptyAsNull(formModel.OfficialRootFolderID);
            setting.PublicFolderID = getEmptyAsNull(formModel.PublicFolderID);
            setting.CommonFolderID = getEmptyAsNull(formModel.CommonFolderID);
            setting.ResponsibleResourcesFolderID = getEmptyAsNull(formModel.ResponsibleResourcesFolderID);
            setting.WorkResourcesFolderID = getEmptyAsNull(formModel.WorkResourcesFolderID);
            setting.OrganizationCalendarID = getEmptyAsNull(formModel.OrganizationCalendarID);
            setting.ResponsibleCalendarID = getEmptyAsNull(formModel.ResponsibleCalendarID);
            setting.OrganizationTaskListID = getEmptyAsNull(formModel.OrganizationTaskListID);
            setting.ResponsibleTaskListID = getEmptyAsNull(formModel.ResponsibleTaskListID);
            setting.NoticeFileID = getEmptyAsNull(formModel.NoticeFileID);
            setting.WorkGuidelineFileID = getEmptyAsNull(formModel.WorkGuidelineFileID);
            setting.ShowTodoAuthor = formModel.ShowTodoAuthor;
            setting.ShowCalendarAuthor = formModel.ShowCalendarAuthor;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private async Task<GlobalSetting> getGlobalSettingAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
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
        if (string.IsNullOrWhiteSpace(formModel.TeamKey))
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
        team.TeamKey = formModel.TeamKey.Trim();
        team.DisplayName = formModel.DisplayName.Trim();
        team.IsActive = formModel.IsActive;
        team.SortOrder = formModel.SortOrder;
        team.MaterialsFolderID = getEmptyAsNull(formModel.MaterialsFolderID);
        team.ApprovedFolderID = getEmptyAsNull(formModel.ApprovedFolderID);
        team.CalendarID = getEmptyAsNull(formModel.CalendarID);
        team.TaskListID = getEmptyAsNull(formModel.TaskListID);
    }

    private GlobalSettingFormModel createGlobalSettingFormModel(GlobalSetting setting)
    {
        GlobalSettingFormModel formModel = new GlobalSettingFormModel();
        formModel.ID = setting.ID;
        formModel.OfficialRootFolderID = getNullAsEmpty(setting.OfficialRootFolderID);
        formModel.PublicFolderID = getNullAsEmpty(setting.PublicFolderID);
        formModel.CommonFolderID = getNullAsEmpty(setting.CommonFolderID);
        formModel.ResponsibleResourcesFolderID = getNullAsEmpty(setting.ResponsibleResourcesFolderID);
        formModel.WorkResourcesFolderID = getNullAsEmpty(setting.WorkResourcesFolderID);
        formModel.OrganizationCalendarID = getNullAsEmpty(setting.OrganizationCalendarID);
        formModel.ResponsibleCalendarID = getNullAsEmpty(setting.ResponsibleCalendarID);
        formModel.OrganizationTaskListID = getNullAsEmpty(setting.OrganizationTaskListID);
        formModel.ResponsibleTaskListID = getNullAsEmpty(setting.ResponsibleTaskListID);
        formModel.NoticeFileID = getNullAsEmpty(setting.NoticeFileID);
        formModel.WorkGuidelineFileID = getNullAsEmpty(setting.WorkGuidelineFileID);
        formModel.ShowTodoAuthor = setting.ShowTodoAuthor;
        formModel.ShowCalendarAuthor = setting.ShowCalendarAuthor;
        return formModel;
    }

    private string? getEmptyAsNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }
}
