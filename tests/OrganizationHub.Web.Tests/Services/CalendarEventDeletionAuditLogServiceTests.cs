using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class CalendarEventDeletionAuditLogServiceTests
{
    [Fact]
    public async Task ManagementCanReviewDeletionAuditButTeamUserCannotAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedPermissionUsersAsync(dbContextFactory);
            CalendarEventDeletionAuditLogService auditLogService = new CalendarEventDeletionAuditLogService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession teamLeaderSession = createUserSession(EStoredCompatibilityRole.TeamLeader);
            UserSession managementSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(9),
                    Username = Username.CreateOptional("management.staff"),
                    DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                    OrganizationTeamIdOrNull = null,
                    TeamKey = OrganizationTeamKey.Empty,
                    TeamDisplayName = DisplayName.CreateOptional(string.Empty)
                });
            UserSession teamUserSession = createUserSession(EStoredCompatibilityRole.TeamUser);
            CalendarEventItem calendarEvent = createCalendarEvent();

            CalendarEventDeletionAuditLogId auditLogId = await auditLogService.RecordPendingDeletionAsync(teamLeaderSession, calendarEvent, CancellationToken.None);
            await auditLogService.MarkDeletionSucceededAsync(auditLogId, CancellationToken.None);

            IReadOnlyList<CalendarEventDeletionAuditLogItem> managementAuditLogs = await auditLogService.GetRecentDeletedEventsAsync(managementSession, new RecentItemLimit(20), CancellationToken.None);
            IReadOnlyList<CalendarEventDeletionAuditLogItem> teamUserAuditLogs = await auditLogService.GetRecentDeletedEventsAsync(teamUserSession, new RecentItemLimit(20), CancellationToken.None);

            CalendarEventDeletionAuditLogItem auditLog = Assert.Single(managementAuditLogs);
            Assert.Equal(ECalendarEventDeletionAuditStatus.Succeeded, auditLog.Status);
            Assert.Equal("organization-event-1", auditLog.EventId);
            Assert.Equal("기획팀 리허설", auditLog.EventSummary);
            Assert.Equal("기획 책임자", auditLog.DeletedByDisplayName);
            Assert.Equal("기획팀 책임자", auditLog.DeletedByPositionName);
            Assert.Empty(teamUserAuditLogs);
        }
    }

    [Fact]
    public async Task FailedDeletionKeepsErrorAndOriginalEventSnapshotAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            await seedPermissionUsersAsync(dbContextFactory);
            CalendarEventDeletionAuditLogService auditLogService = new CalendarEventDeletionAuditLogService(dbContextFactory, new OrganizationPermissionService(dbContextFactory));
            UserSession teamLeaderSession = createUserSession(EStoredCompatibilityRole.TeamLeader);
            UserSession managementSession = new UserSession(
                new UserSessionCreateRequest
                {
                    UserAccountId = UserAccountId.CreateExisting(9),
                    Username = Username.CreateOptional("management.staff"),
                    DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                    StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                    OrganizationTeamIdOrNull = null,
                    TeamKey = OrganizationTeamKey.Empty,
                    TeamDisplayName = DisplayName.CreateOptional(string.Empty)
                });
            CalendarEventItem calendarEvent = createCalendarEvent();

            CalendarEventDeletionAuditLogId auditLogId = await auditLogService.RecordPendingDeletionAsync(teamLeaderSession, calendarEvent, CancellationToken.None);
            await auditLogService.MarkDeletionFailedAsync(auditLogId, CalendarEventDeletionFailureMessage.CreateOptional("Google Calendar deletion failed."), CancellationToken.None);

            IReadOnlyList<CalendarEventDeletionAuditLogItem> auditLogs = await auditLogService.GetRecentDeletedEventsAsync(managementSession, new RecentItemLimit(20), CancellationToken.None);

            CalendarEventDeletionAuditLogItem auditLog = Assert.Single(auditLogs);
            Assert.Equal(ECalendarEventDeletionAuditStatus.Failed, auditLog.Status);
            Assert.Equal("Google Calendar deletion failed.", auditLog.ErrorMessage);
            Assert.Equal("planning-calendar", auditLog.CalendarId);
            Assert.Equal(new DateTimeOffset(2026, 5, 23, 9, 0, 0, TimeSpan.FromHours(9)), auditLog.EventStart);
            Assert.Equal(new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.FromHours(9)), auditLog.EventEnd);
        }
    }

    private static UserSession createUserSession(EStoredCompatibilityRole role)
    {
        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(7),
                Username = Username.CreateOptional("planning-lead"),
                DisplayName = DisplayName.CreateOptional("기획 책임자"),
                StoredCompatibilityRole = role,
                OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                TeamKey = new OrganizationTeamKey("Planning"),
                TeamDisplayName = DisplayName.CreateOptional("기획팀")
            });
    }

    private static async Task seedPermissionUsersAsync(TestDbContextFactory dbContextFactory)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            Team planningTeam = createTeam(1, "Planning", "기획팀", 10);
            Team managementTeam = createTeam(2, "Management", "관리팀", 20);
            OrganizationPosition teamMemberPosition = createPosition(1, "기획팀 구성원", 10);
            OrganizationPosition teamLeaderPosition = createPosition(1, "기획팀 책임자", 20);
            OrganizationPosition auditReaderPosition = createPosition(2, "일정 감사 담당", 10);
            addPermissionGrant(auditReaderPosition, PermissionKeys.CALENDAR_AUDIT_READ, EPermissionScopeType.Global);

            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(managementTeam);
            dbContext.Positions.Add(teamMemberPosition);
            dbContext.Positions.Add(teamLeaderPosition);
            dbContext.Positions.Add(auditReaderPosition);
            dbContext.UserAccounts.Add(createUser(7, "planning-lead", "기획 책임자", EStoredCompatibilityRole.TeamLeader, 1, teamLeaderPosition));
            dbContext.UserAccounts.Add(createUser(8, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1, teamMemberPosition));
            dbContext.UserAccounts.Add(createUser(9, "management.staff", "관리팀 담당자", EStoredCompatibilityRole.ManagementStaff, 2, auditReaderPosition));
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static Team createTeam(int id, string teamKey, string displayName, int sortOrder)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private static OrganizationPosition createPosition(int teamId, string name, int sortOrder)
    {
        OrganizationPosition position = new OrganizationPosition();
        position.TeamId = teamId;
        position.Name = name;
        position.Description = string.Empty;
        position.SortOrder = sortOrder;
        position.IsActive = true;
        return position;
    }

    private static void addPermissionGrant(OrganizationPosition position, string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
        permissionGrant.Position = position;
        permissionGrant.PermissionKey = permissionKey;
        permissionGrant.ScopeType = scopeType;
        permissionGrant.IsAllowed = true;
        position.PermissionGrants.Add(permissionGrant);
    }

    private static UserAccount createUser(int id, string username, string displayName, EStoredCompatibilityRole role, int teamId, OrganizationPosition position)
    {
        UserAccount userAccount = new UserAccount();
        userAccount.Id = id;
        userAccount.Username = username;
        userAccount.DisplayName = displayName;
        userAccount.PasswordHash = "test";
        userAccount.StoredCompatibilityRole = role;
        userAccount.TeamId = teamId;
        userAccount.Position = position;
        userAccount.IsActive = true;
        userAccount.CreatedAt = DateTime.UtcNow;
        return userAccount;
    }

    private static CalendarEventItem createCalendarEvent()
    {
        CalendarEventItem calendarEvent = new CalendarEventItem();
        calendarEvent.Id = "organization-event-1";
        calendarEvent.CalendarId = "planning-calendar";
        calendarEvent.CalendarName = "기획팀";
        calendarEvent.Summary = "기획팀 리허설";
        calendarEvent.Location = "강당";
        calendarEvent.Start = new DateTimeOffset(2026, 5, 23, 9, 0, 0, TimeSpan.FromHours(9));
        calendarEvent.End = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.FromHours(9));
        calendarEvent.IsAllDay = false;
        return calendarEvent;
    }
}
