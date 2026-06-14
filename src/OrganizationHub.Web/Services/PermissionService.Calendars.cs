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
        calendars.Add(new CalendarReference("all", "전체", ECalendarViewKind.Aggregate, null));
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            bool canReadGlobalCalendars = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_READ), null, usesConfiguredPermissions, userSession.IsManagementRole(), cancellationToken);
            if (canReadGlobalCalendars && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.OrganizationCalendarID, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            bool canReadResponsibleCalendar = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_READ), null, usesConfiguredPermissions, userSession.IsTeamLeaderOrHigherRole(), cancellationToken);
            if (canReadResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.ResponsibleCalendarID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarID != null && team.CalendarID != string.Empty).OrderBy(team => team.SortOrder);
            if (usesConfiguredPermissions == false && userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    return calendars;
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarID))
                {
                    continue;
                }

                bool canReadTeamCalendar = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_READ), team.ID, usesConfiguredPermissions, canReadMaterialsForTeam(userSession, team), cancellationToken);
                if (canReadTeamCalendar)
                {
                    calendars.Add(new CalendarReference(team.CalendarID, team.DisplayName, ECalendarViewKind.Team, team.ID));
                }
            }
        }

        return calendars;
    }

    public async Task<IReadOnlyList<CalendarReference>> GetWritableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        List<CalendarReference> calendars = new List<CalendarReference>();
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            bool canWriteManagementCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(userSession, null, usesConfiguredPermissions, userSession.IsManagementRole(), cancellationToken);
            if (canWriteManagementCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.OrganizationCalendarID, OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            bool canWriteResponsibleCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(userSession, null, usesConfiguredPermissions, UserPermissionPolicy.CanWriteResponsibleWork(userSession.Role), cancellationToken);
            if (canWriteResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarID) == false)
            {
                calendars.Add(new CalendarReference(settingOrNull.ResponsibleCalendarID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarID != null && team.CalendarID != string.Empty).OrderBy(team => team.SortOrder);
            if (usesConfiguredPermissions == false && UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role) == false)
            {
                if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
                {
                    return calendars;
                }

                if (userSession.TeamID.HasValue == false)
                {
                    return calendars;
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarID))
                {
                    continue;
                }

                bool canWriteTeamCalendar = await canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(userSession, team.ID, usesConfiguredPermissions, canWriteTeamWorkFallback(userSession, team), cancellationToken);
                if (canWriteTeamCalendar)
                {
                    calendars.Add(new CalendarReference(team.CalendarID, team.DisplayName, ECalendarViewKind.Team, team.ID));
                }
            }
        }

        return calendars;
    }

    public async Task<bool> CanWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideWriteCalendarAsync(userSession, calendarID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        IReadOnlyList<CalendarReference> calendars = await GetWritableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference calendar in calendars)
        {
            if (calendar.CalendarID == calendarID)
            {
                return PermissionDecision.Allow(calendar.DisplayName);
            }
        }

        IReadOnlyList<CalendarReference> readableCalendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        CalendarReference? readableCalendarOrNull = readableCalendars.FirstOrDefault(calendar => calendar.CalendarID == calendarID);
        if (readableCalendarOrNull != null)
        {
            return PermissionDecision.Deny("조회만 가능한 일정입니다.", readableCalendarOrNull.DisplayName);
        }

        return PermissionDecision.Deny("접근할 수 없는 일정입니다.", "일정");
    }

    public async Task<bool> CanReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await DecideReadCalendarAsync(userSession, calendarID, cancellationToken);
        return decision.IsAllowed;
    }

    public async Task<PermissionDecision> DecideReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken)
    {
        if (calendarID == "all")
        {
            return PermissionDecision.Allow("전체 일정");
        }

        IReadOnlyList<CalendarReference> calendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference calendar in calendars)
        {
            if (calendar.CalendarID == calendarID)
            {
                return PermissionDecision.Allow(calendar.DisplayName);
            }
        }

        return PermissionDecision.Deny("접근할 수 없는 일정입니다.", "일정");
    }
}
