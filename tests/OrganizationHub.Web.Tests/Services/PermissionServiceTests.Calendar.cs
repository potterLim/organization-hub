using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class PermissionServiceTests
{
    [Fact]
    public async Task CalendarPolicySeparatesTeamMemberLeaderAndManagementAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession teamUserSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);
                UserSession responsibleSession = createPlanningUserSession(EStoredCompatibilityRole.TeamLeader);
                UserSession managementSession = new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(2),
                        Username = Username.CreateOptional("management.staff"),
                        DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                        StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                        OrganizationTeamIdOrNull = null,
                        TeamKey = OrganizationTeamKey.Empty,
                        TeamDisplayName = DisplayName.CreateOptional(string.Empty)
                    });
                UserSession directorSession = new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(3),
                        Username = Username.CreateOptional("director"),
                        DisplayName = DisplayName.CreateOptional("Director"),
                        StoredCompatibilityRole = EStoredCompatibilityRole.ManagementDirector,
                        OrganizationTeamIdOrNull = null,
                        TeamKey = OrganizationTeamKey.Empty,
                        TeamDisplayName = DisplayName.CreateOptional(string.Empty)
                    });

                IReadOnlyList<CalendarReference> teamUserReadableCalendars = await permissionService.GetReadableCalendarsAsync(teamUserSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> leaderReadableCalendars = await permissionService.GetReadableCalendarsAsync(responsibleSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> leaderWritableCalendars = await permissionService.GetWritableCalendarsAsync(responsibleSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> managementReadableCalendars = await permissionService.GetReadableCalendarsAsync(managementSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> managementWritableCalendars = await permissionService.GetWritableCalendarsAsync(managementSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> directorWritableCalendars = await permissionService.GetWritableCalendarsAsync(directorSession, CancellationToken.None);

                Assert.True(hasCalendar(teamUserReadableCalendars, "all"));
                Assert.Equal("전체", getCalendarDisplayName(teamUserReadableCalendars, "all"));
                Assert.True(hasCalendar(teamUserReadableCalendars, "planning-calendar"));
                Assert.False(hasCalendar(teamUserReadableCalendars, "responsible-calendar"));
                Assert.False(hasCalendar(teamUserReadableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(leaderReadableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(leaderWritableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(leaderWritableCalendars, "planning-calendar"));
                Assert.True(hasCalendar(leaderWritableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(managementReadableCalendars, "management-calendar"));
                Assert.True(hasCalendar(managementReadableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(managementReadableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(managementWritableCalendars, "management-calendar"));
                Assert.True(hasCalendar(managementWritableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(managementWritableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(directorWritableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(directorWritableCalendars, "operation-calendar"));
            }
        }
    }

    [Fact]
    public async Task ConfiguredPositionGrantsOwnTeamCalendarReadAndWriteAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            UserSession userSession = await seedConfiguredPermissionUserAsync(dbContextFactory, 42, 1, EPermissionScopeType.OwnTeam, new List<string> { PermissionKeys.CALENDAR_READ, PermissionKeys.CALENDAR_CREATE });
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));

                IReadOnlyList<CalendarReference> readableCalendars = await permissionService.GetReadableCalendarsAsync(userSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> writableCalendars = await permissionService.GetWritableCalendarsAsync(userSession, CancellationToken.None);
                bool canWritePlanningCalendar = await permissionService.CanWriteCalendarAsync(userSession, calendarId("planning-calendar"), CancellationToken.None);
                bool canWriteOperationCalendar = await permissionService.CanWriteCalendarAsync(userSession, calendarId("operation-calendar"), CancellationToken.None);

                Assert.True(hasCalendar(readableCalendars, "planning-calendar"));
                Assert.False(hasCalendar(readableCalendars, "operation-calendar"));
                Assert.False(hasCalendar(readableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(writableCalendars, "planning-calendar"));
                Assert.False(hasCalendar(writableCalendars, "operation-calendar"));
                Assert.True(canWritePlanningCalendar);
                Assert.False(canWriteOperationCalendar);
            }
        }
    }
}
