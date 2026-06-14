using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleHealthCheckService
{
    private async Task addCalendarItemsAsync(
        List<GoogleHealthCheckItem> items,
        GlobalSetting setting,
        List<Team> teams,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleCalendarListItem> calendars =
            await mGoogleWorkspaceBrowserService.ListCalendarsAsync(cancellationToken);
        HashSet<string> calendarIds = new HashSet<string>(calendars.Select(calendar => calendar.Id));
        addCalendarItem(
            items,
            "Calendar",
            OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME + " Calendar",
            setting.OrganizationCalendarId,
            calendarIds);
        addCalendarItem(
            items,
            "Calendar",
            OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME + " Calendar",
            setting.ResponsibleCalendarId,
            calendarIds);

        foreach (Team team in teams)
        {
            addCalendarItem(
                items,
                "팀 Calendar",
                team.DisplayName + " Calendar",
                team.CalendarId,
                calendarIds);
        }
    }

    private void addCalendarItem(
        List<GoogleHealthCheckItem> items,
        string area,
        string name,
        string? calendarIdOrNull,
        HashSet<string> calendarIds)
    {
        if (string.IsNullOrWhiteSpace(calendarIdOrNull))
        {
            addItem(items, area, name, STATUS_WARNING, "Calendar가 연결되지 않았습니다.");
            return;
        }

        bool hasCalendar = calendarIds.Contains(calendarIdOrNull);
        addItem(
            items,
            area,
            name,
            hasCalendar ? STATUS_HEALTHY : STATUS_ERROR,
            hasCalendar ? "연결된 계정에서 확인되었습니다." : "연결된 계정에서 이 Calendar를 찾을 수 없습니다.");
    }
}
