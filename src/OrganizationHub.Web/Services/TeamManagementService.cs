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

internal sealed class TeamManagementService : ITeamManagementService
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
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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

            if (teamOrNull.Revision != formModel.Revision)
            {
                return AdministrativeSaveUtility.CreateConflictResult();
            }
            applyTeamForm(teamOrNull, formModel);
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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
            if (OrderUpdateRequestValidation.hasDuplicateValues(request.OrderedTeamIds))
            {
                return ServiceResult.Fail("정렬할 팀이 중복되어 있습니다.");
            }

            List<int> orderedTeamIdValues = request.OrderedTeamIds.Select(teamId => teamId.GetRequiredValue()).ToList();
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

            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
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

    private string? getTrimmedTextOrNullIfBlank(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }

}
