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
using GoogleCalendarApi = Google.Apis.Calendar.v3.CalendarService;
using GoogleCalendarData = Google.Apis.Calendar.v3.Data.Calendar;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private const string CALENDAR_LIST_CACHE_KEY = "OrganizationHub:GoogleWorkspace:Calendars";
    private static readonly string[] CALENDAR_SCOPES = new string[] { GoogleCalendarApi.Scope.Calendar };

    public async Task<IReadOnlyList<GoogleCalendarAssignmentOption>> GetCalendarAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        List<GoogleCalendarAssignmentOption> options = new List<GoogleCalendarAssignmentOption>();
        options.Add(createCalendarAssignmentOption(GoogleWorkspaceAssignmentTargetKey.OrganizationResource, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME + " Calendar", EGoogleCalendarAssignmentKind.Organization, null));
        options.Add(createCalendarAssignmentOption(GoogleWorkspaceAssignmentTargetKey.ResponsibleResource, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME + " Calendar", EGoogleCalendarAssignmentKind.Responsible, null));

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            foreach (Team team in teams)
            {
                OrganizationTeamId teamId = OrganizationTeamId.CreateExisting(team.Id);
                options.Add(createCalendarAssignmentOption(GoogleWorkspaceAssignmentTargetKey.CreateTeamResource(teamId), team.DisplayName + " Calendar", EGoogleCalendarAssignmentKind.Team, teamId));
            }
        }

        return options;
    }

    public async Task<ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>> EnsureStandardCalendarsAsync(CancellationToken cancellationToken)
    {
        if (mCredentialFactoryOrNull == null || mOptionsOrNull == null)
        {
            return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Fail("Calendar 생성 서비스를 사용할 수 없습니다.");
        }

        GoogleWorkspaceResourceSetupSummary summary = new GoogleWorkspaceResourceSetupSummary();
        List<string> failureMessages = new List<string>();
        try
        {
            GoogleCalendarApi calendarService = await createCalendarServiceAsync(cancellationToken);
            List<GoogleCalendarData> googleCalendars = await listGoogleCalendarsAsync(calendarService, cancellationToken);
            IReadOnlyList<GoogleCalendarAssignmentOption> options = await GetCalendarAssignmentOptionsAsync(cancellationToken);
            foreach (GoogleCalendarAssignmentOption option in options)
            {
                string standardName = getStandardCalendarName(option);
                GoogleCalendarData calendar = await getOrCreateGoogleCalendarAsync(calendarService, googleCalendars, standardName, summary, cancellationToken);
                if (string.IsNullOrWhiteSpace(calendar.Id))
                {
                    failureMessages.Add(standardName + " Calendar ID를 받지 못했습니다.");
                    continue;
                }

                ServiceResult assignResult = await AssignCalendarAsync(option.TargetKey, CalendarId.CreateOptional(calendar.Id), cancellationToken);
                if (assignResult.IsSuccess)
                {
                    summary.AssignedCount = summary.AssignedCount + 1;
                    continue;
                }

                failureMessages.Add(standardName + " Calendar를 저장하지 못했습니다. " + assignResult.ErrorMessage);
            }

            clearCalendarListCache();
        }
        catch (Exception exception)
        {
            failureMessages.Add(createGoogleCalendarSetupErrorMessage(exception));
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Fail(createResourceSetupFailureMessage("Calendar", failureMessages));
        }

        return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Succeed(summary);
    }

    public async Task<ServiceResult> AssignCalendarAsync(
        GoogleWorkspaceAssignmentTargetKey targetKey,
        CalendarId calendarId,
        CancellationToken cancellationToken
    )
    {
        if (targetKey.HasValue == false)
        {
            return ServiceResult.Fail("연결할 대상을 선택해 주세요.");
        }

        if (calendarId.HasValue == false)
        {
            return ServiceResult.Fail("Calendar를 선택해 주세요.");
        }

        IReadOnlyList<GoogleCalendarAssignmentOption> options = await GetCalendarAssignmentOptionsAsync(cancellationToken);
        GoogleCalendarAssignmentOption? optionOrNull = options.FirstOrDefault(option => targetKey.Matches(option.TargetKey));
        if (optionOrNull == null)
        {
            return ServiceResult.Fail("연결할 대상을 찾을 수 없습니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string requiredCalendarId = calendarId.GetRequiredValue();
            if (optionOrNull.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Organization)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.OrganizationCalendarId = requiredCalendarId;
            }
            else if (optionOrNull.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Responsible)
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                setting.ResponsibleCalendarId = requiredCalendarId;
            }
            else
            {
                int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(optionOrNull.TeamIdOrNull);
                if (teamIdValueOrNull.HasValue == false)
                {
                    return ServiceResult.Fail("팀 Calendar 대상을 찾을 수 없습니다.");
                }

                int teamId = teamIdValueOrNull.GetValueOrDefault();
                Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.Id == teamId, cancellationToken);
                if (teamOrNull == null)
                {
                    return ServiceResult.Fail("팀을 찾을 수 없습니다.");
                }

                teamOrNull.CalendarId = requiredCalendarId;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return ServiceResult.Succeed();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult.Fail("다른 관리자가 설정을 변경했습니다. 최신 설정을 불러온 뒤 다시 적용해 주세요.");
            }
        }
    }

    private GoogleCalendarAssignmentOption createCalendarAssignmentOption(
        GoogleWorkspaceAssignmentTargetKey targetKey,
        string displayName,
        EGoogleCalendarAssignmentKind calendarAssignmentKind,
        OrganizationTeamId? teamIdOrNull
    )
    {
        GoogleCalendarAssignmentOption option = new GoogleCalendarAssignmentOption();
        option.TargetKey = targetKey;
        option.DisplayName = displayName;
        option.CalendarAssignmentKind = calendarAssignmentKind;
        option.TeamIdOrNull = teamIdOrNull;
        return option;
    }
}
