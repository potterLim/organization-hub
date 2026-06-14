using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using GoogleTaskListData = Google.Apis.Tasks.v1.Data.TaskList;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private const string TASK_LIST_CACHE_KEY = "OrganizationHub:GoogleWorkspace:TaskLists";
    private static readonly string[] TASK_SCOPES = new[] { GoogleTasksApi.Scope.Tasks };

    public async Task<IReadOnlyList<GoogleTaskListAssignmentOption>> GetTaskListAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        List<GoogleTaskListAssignmentOption> options = new List<GoogleTaskListAssignmentOption>();
        options.Add(createTaskListAssignmentOption(GoogleWorkspaceAssignmentTargetKey.OrganizationResource, "조직 공용 Google Tasks 목록", EGoogleTaskListAssignmentKind.Organization, null));
        options.Add(createTaskListAssignmentOption(GoogleWorkspaceAssignmentTargetKey.ResponsibleResource, "책임자 Google Tasks 목록", EGoogleTaskListAssignmentKind.Responsible, null));

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            foreach (Team team in teams)
            {
                OrganizationTeamId teamId = OrganizationTeamId.CreateExisting(team.Id);
                options.Add(createTaskListAssignmentOption(GoogleWorkspaceAssignmentTargetKey.CreateTeamResource(teamId), team.DisplayName + " Google Tasks 목록", EGoogleTaskListAssignmentKind.Team, teamId));
            }
        }

        return options;
    }

    public async Task<ServiceResultOfT<GoogleTaskListSetupSummary>> EnsureStandardTaskListsAsync(CancellationToken cancellationToken)
    {
        if (mCredentialFactoryOrNull == null || mOptionsOrNull == null)
        {
            return ServiceResultOfT<GoogleTaskListSetupSummary>.Fail("Google Tasks 목록 생성 서비스를 사용할 수 없습니다.");
        }

        GoogleTaskListSetupSummary summary = new GoogleTaskListSetupSummary();
        List<string> failureMessages = new List<string>();
        try
        {
            GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
            List<GoogleTaskListData> googleTaskLists = await listGoogleTaskListsAsync(tasksService, cancellationToken);
            IReadOnlyList<GoogleTaskListAssignmentOption> options = await GetTaskListAssignmentOptionsAsync(cancellationToken);
            foreach (GoogleTaskListAssignmentOption option in options)
            {
                summary.TargetCount = summary.TargetCount + 1;
                string standardName = getStandardTaskListName(option);
                GoogleTaskListData taskList = await getOrCreateGoogleTaskListAsync(tasksService, googleTaskLists, standardName, summary, cancellationToken);
                if (string.IsNullOrWhiteSpace(taskList.Id))
                {
                    failureMessages.Add(standardName + " 목록 ID를 받지 못했습니다.");
                    continue;
                }

                ServiceResult assignResult = await AssignTaskListAsync(option.TargetKey, TodoTaskListId.CreateOptional(taskList.Id), cancellationToken);
                if (assignResult.IsSuccess)
                {
                    summary.AssignedCount = summary.AssignedCount + 1;
                    continue;
                }

                failureMessages.Add(standardName + " 목록을 저장하지 못했습니다. " + assignResult.ErrorMessage);
            }

            clearTaskListCache();
        }
        catch (Exception exception)
        {
            return ServiceResultOfT<GoogleTaskListSetupSummary>.Fail(createGoogleTasksSetupErrorMessage(exception));
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleTaskListSetupSummary>.Fail(createTaskListSetupFailureMessage(failureMessages));
        }

        return ServiceResultOfT<GoogleTaskListSetupSummary>.Succeed(summary);
    }

    public async Task<ServiceResult> AssignTaskListAsync(
        GoogleWorkspaceAssignmentTargetKey targetKey,
        TodoTaskListId taskListId,
        CancellationToken cancellationToken
    )
    {
        if (targetKey.HasValue == false)
        {
            return ServiceResult.Fail("저장 대상을 선택해 주세요.");
        }

        if (taskListId.HasValue == false)
        {
            return ServiceResult.Fail("Google Tasks 목록을 선택해 주세요.");
        }

        IReadOnlyList<GoogleTaskListAssignmentOption> options = await GetTaskListAssignmentOptionsAsync(cancellationToken);
        GoogleTaskListAssignmentOption? optionOrNull = options.FirstOrDefault(option => targetKey.Matches(option.TargetKey));
        if (optionOrNull == null)
        {
            return ServiceResult.Fail("저장 대상을 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string requiredTaskListId = taskListId.GetRequiredValue();
            if (optionOrNull.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Organization)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.OrganizationTaskListId = requiredTaskListId;
            }
            else if (optionOrNull.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Responsible)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.ResponsibleTaskListId = requiredTaskListId;
            }
            else
            {
                int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(optionOrNull.TeamIdOrNull);
                if (teamIdValueOrNull.HasValue == false)
                {
                    return ServiceResult.Fail("팀 Google Tasks 목록 대상을 찾을 수 없습니다.");
                }

                int teamId = teamIdValueOrNull.GetValueOrDefault();
                Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken);
                if (teamOrNull == null)
                {
                    return ServiceResult.Fail("팀을 찾을 수 없습니다.");
                }

                teamOrNull.TaskListId = requiredTaskListId;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    private GoogleTaskListAssignmentOption createTaskListAssignmentOption(
        GoogleWorkspaceAssignmentTargetKey targetKey,
        string displayName,
        EGoogleTaskListAssignmentKind taskListAssignmentKind,
        OrganizationTeamId? teamIdOrNull
    )
    {
        GoogleTaskListAssignmentOption option = new GoogleTaskListAssignmentOption();
        option.TargetKey = targetKey;
        option.DisplayName = displayName;
        option.TaskListAssignmentKind = taskListAssignmentKind;
        option.TeamIdOrNull = teamIdOrNull;
        return option;
    }
}
