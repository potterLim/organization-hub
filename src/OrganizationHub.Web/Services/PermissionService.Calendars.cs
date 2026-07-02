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

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            bool canReadGlobalCalendars = await canUsePermissionAsync(
                userSession,
                new PermissionKey(PermissionKeys.CALENDAR_READ),
                null,
                cancellationToken);
            if (canReadGlobalCalendars && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.OrganizationCalendarId), OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            bool canReadResponsibleCalendar = await canUsePermissionAsync(
                userSession,
                new PermissionKey(PermissionKeys.CALENDAR_READ),
                null,
                cancellationToken);
            if (canReadResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.ResponsibleCalendarId), OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarId != null && team.CalendarId != string.Empty).OrderBy(team => team.SortOrder);
            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarId))
                {
                    continue;
                }

                bool canReadTeamCalendar = await canUsePermissionAsync(
                    userSession,
                    new PermissionKey(PermissionKeys.CALENDAR_READ),
                    OrganizationTeamId.CreateExisting(team.Id),
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
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.Id).FirstOrDefaultAsync(cancellationToken);
            bool canWriteManagementCalendar = await canUseAnyCalendarWritePermissionAsync(
                userSession,
                null,
                cancellationToken);
            if (canWriteManagementCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.OrganizationCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.OrganizationCalendarId), OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME, ECalendarViewKind.Organization, null));
            }

            bool canWriteResponsibleCalendar = await canUseAnyCalendarWritePermissionAsync(
                userSession,
                null,
                cancellationToken);
            if (canWriteResponsibleCalendar && settingOrNull != null && string.IsNullOrWhiteSpace(settingOrNull.ResponsibleCalendarId) == false)
            {
                calendars.Add(new CalendarReference(CalendarId.CreateOptional(settingOrNull.ResponsibleCalendarId), OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME, ECalendarViewKind.Responsible, null));
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive && team.CalendarId != null && team.CalendarId != string.Empty).OrderBy(team => team.SortOrder);
            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.CalendarId))
                {
                    continue;
                }

                bool canWriteTeamCalendar = await canUseAnyCalendarWritePermissionAsync(
                    userSession,
                    OrganizationTeamId.CreateExisting(team.Id),
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
                return allowPermission(createPermissionScopeLabel(calendar.DisplayName));
            }
        }

        IReadOnlyList<CalendarReference> readableCalendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        CalendarReference? readableCalendarOrNull = readableCalendars.FirstOrDefault(calendar => calendar.CalendarId == calendarId);
        if (readableCalendarOrNull != null)
        {
            return denyPermission(
                createPermissionDecisionReason("조회만 가능한 일정입니다."),
                createPermissionScopeLabel(readableCalendarOrNull.DisplayName));
        }

        return denyPermission(
            createPermissionDecisionReason("접근할 수 없는 일정입니다."),
            createPermissionScopeLabel("일정"));
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
            return allowPermission(createPermissionScopeLabel("전체 일정"));
        }

        IReadOnlyList<CalendarReference> calendars = await GetReadableCalendarsAsync(userSession, cancellationToken);
        foreach (CalendarReference calendar in calendars)
        {
            if (calendar.CalendarId == calendarId)
            {
                return allowPermission(createPermissionScopeLabel(calendar.DisplayName));
            }
        }

        return denyPermission(
            createPermissionDecisionReason("접근할 수 없는 일정입니다."),
            createPermissionScopeLabel("일정"));
    }
}
