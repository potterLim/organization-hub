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

public sealed partial class PermissionService
{
    public async Task<IReadOnlyList<CalendarReference>> GetReadableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        List<CalendarReference> calendars = new List<CalendarReference>();
        calendars.Add(new CalendarReference(CalendarId.AllCalendars, "전체", ECalendarViewKind.Aggregate, null));
        EPermissionConfigurationMode configurationMode = await getPermissionConfigurationModeAsync(userSession, cancellationToken);

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            EPermissionFallback globalReadFallback = PermissionFallbackUtility.FromDecision(userSession.IsManagementRole());
            bool canReadGlobalCalendars = await canUseConfiguredOrFallbackPermissionAsync(
                userSession,
                new PermissionKey(PermissionKeys.CALENDAR_READ),
                null,
                configurationMode,
                globalReadFallback,
                cancellationToken);
            if (canReadGlobalCalendars && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.OrganizationCalendarId), OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            EPermissionFallback responsibleReadFallback = PermissionFallbackUtility.FromDecision(userSession.IsTeamLeaderOrHigherRole());
            bool canReadResponsibleCalendar = await canUseConfiguredOrFallbackPermissionAsync(
                userSession,
                new PermissionKey(PermissionKeys.CALENDAR_READ),
                null,
                configurationMode,
                responsibleReadFallback,
                cancellationToken);
            if (canReadResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.ResponsibleCalendarId), OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarId != null && team.CalendarId != string.Empty).OrderBy(team => team.SortOrder);
            if (configurationMode == EPermissionConfigurationMode.FallbackPolicy && userSession.IsManagementRole() == false)
            {
                if (userSession.HasTeam == false)
                {
                    return calendars;
                }

                int teamId = userSession.GetRequiredTeamIdValue();
                teamQuery = teamQuery.Where(team => team.Id == teamId);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarId))
                {
                    continue;
                }

                EPermissionFallback teamReadFallback = PermissionFallbackUtility.FromDecision(canReadMaterialsForTeam(userSession, team));
                bool canReadTeamCalendar = await canUseConfiguredOrFallbackPermissionAsync(
                    userSession,
                    new PermissionKey(PermissionKeys.CALENDAR_READ),
                    OrganizationTeamId.CreateExisting(team.Id),
                    configurationMode,
                    teamReadFallback,
                    cancellationToken);
                if (canReadTeamCalendar)
                {
                    calendars.Add(new CalendarReference(CalendarId.CreateOptional(team.CalendarId), team.DisplayName, ECalendarViewKind.Team, OrganizationTeamId.CreateExisting(team.Id)));
                }
            }
        }

        return calendars;
    }

    public async Task<IReadOnlyList<CalendarReference>> GetWritableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        List<CalendarReference> calendars = new List<CalendarReference>();
        EPermissionConfigurationMode configurationMode = await getPermissionConfigurationModeAsync(userSession, cancellationToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            EPermissionFallback managementWriteFallback = PermissionFallbackUtility.FromDecision(userSession.IsManagementRole());
            bool canWriteManagementCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(
                userSession,
                null,
                configurationMode,
                managementWriteFallback,
                cancellationToken);
            if (canWriteManagementCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.OrganizationCalendarId), OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            EPermissionFallback responsibleWriteFallback = PermissionFallbackUtility.FromDecision(UserPermissionPolicy.CanWriteResponsibleWork(userSession.Role));
            bool canWriteResponsibleCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(
                userSession,
                null,
                configurationMode,
                responsibleWriteFallback,
                cancellationToken);
            if (canWriteResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.ResponsibleCalendarId), OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarId != null && team.CalendarId != string.Empty).OrderBy(team => team.SortOrder);
            if (configurationMode == EPermissionConfigurationMode.FallbackPolicy && UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role) == false)
            {
                if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
                {
                    return calendars;
                }

                if (userSession.HasTeam == false)
                {
                    return calendars;
                }

                int teamId = userSession.GetRequiredTeamIdValue();
                teamQuery = teamQuery.Where(team => team.Id == teamId);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarId))
                {
                    continue;
                }

                EPermissionFallback teamWriteFallback = PermissionFallbackUtility.FromDecision(canWriteTeamWorkFallback(userSession, team));
                bool canWriteTeamCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(
                    userSession,
                    OrganizationTeamId.CreateExisting(team.Id),
                    configurationMode,
                    teamWriteFallback,
                    cancellationToken);
                if (canWriteTeamCalendar)
                {
                    calendars.Add(new CalendarReference(CalendarId.CreateOptional(team.CalendarId), team.DisplayName, ECalendarViewKind.Team, OrganizationTeamId.CreateExisting(team.Id)));
                }
            }
        }

        return calendars;
    }

    public async Task<bool> CanWriteCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideWriteCalendarAsync(userSession, calendarId, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideWriteCalendarAsync(
        UserSession userSession,
        CalendarId calendarId,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<CalendarReference> calendars = await GetWritableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference calendar in calendars)
        {
            if (calendar.CalendarId == calendarId)
            {
                return PermissionDecision.Allow(calendar.DisplayName);
            }
        }

        IReadOnlyList<CalendarReference> readableCalendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        CalendarReference? readableCalendarOrNull = readableCalendars.FirstOrDefault(calendar => calendar.CalendarId == calendarId);
        if (readableCalendarOrNull != null)
        {
            return PermissionDecision.Deny("조회만 가능한 일정입니다.", readableCalendarOrNull.DisplayName);
        }

        return PermissionDecision.Deny("접근할 수 없는 일정입니다.", "일정");
    }

    public async Task<bool> CanReadCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideReadCalendarAsync(userSession, calendarId, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideReadCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken)
    {
        if (calendarId.IsAggregate)
        {
            return PermissionDecision.Allow("전체 일정");
        }

        IReadOnlyList<CalendarReference> calendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference calendar in calendars)
        {
            if (calendar.CalendarId == calendarId)
            {
                return PermissionDecision.Allow(calendar.DisplayName);
            }
        }

        return PermissionDecision.Deny("접근할 수 없는 일정입니다.", "일정");
    }
}
