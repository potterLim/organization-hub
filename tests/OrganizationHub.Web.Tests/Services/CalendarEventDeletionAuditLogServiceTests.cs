using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            CalendarEventDeletionAuditLogService auditLogService = new CalendarEventDeletionAuditLogService(dbContextFactory);
            UserSession teamLeaderSession = createUserSession(EUserRole.TeamLeader);
            UserSession managementSession = new UserSession(9, "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, null, string.Empty, string.Empty);
            UserSession teamUserSession = createUserSession(EUserRole.TeamUser);
            CalendarEventItem calendarEvent = createCalendarEvent();

            int auditLogID = await auditLogService.RecordPendingDeletionAsync(teamLeaderSession, calendarEvent, CancellationToken.None);
            await auditLogService.MarkDeletionSucceededAsync(auditLogID, CancellationToken.None);

            IReadOnlyList<CalendarEventDeletionAuditLogItem> managementAuditLogs = await auditLogService.GetRecentDeletedEventsAsync(managementSession, 20, CancellationToken.None);
            IReadOnlyList<CalendarEventDeletionAuditLogItem> teamUserAuditLogs = await auditLogService.GetRecentDeletedEventsAsync(teamUserSession, 20, CancellationToken.None);

            CalendarEventDeletionAuditLogItem auditLog = Assert.Single(managementAuditLogs);
            Assert.Equal(ECalendarEventDeletionAuditStatus.Succeeded, auditLog.Status);
            Assert.Equal("organization-event-1", auditLog.EventID);
            Assert.Equal("기획팀 리허설", auditLog.EventSummary);
            Assert.Equal("기획 책임자", auditLog.DeletedByDisplayName);
            Assert.Empty(teamUserAuditLogs);
        }
    }

    [Fact]
    public async Task FailedDeletionKeepsErrorAndOriginalEventSnapshotAsync()
    {
        using (TestDbContextFactory dbContextFactory = new TestDbContextFactory())
        {
            CalendarEventDeletionAuditLogService auditLogService = new CalendarEventDeletionAuditLogService(dbContextFactory);
            UserSession teamLeaderSession = createUserSession(EUserRole.TeamLeader);
            UserSession managementSession = new UserSession(9, "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, null, string.Empty, string.Empty);
            CalendarEventItem calendarEvent = createCalendarEvent();

            int auditLogID = await auditLogService.RecordPendingDeletionAsync(teamLeaderSession, calendarEvent, CancellationToken.None);
            await auditLogService.MarkDeletionFailedAsync(auditLogID, "Google Calendar deletion failed.", CancellationToken.None);

            IReadOnlyList<CalendarEventDeletionAuditLogItem> auditLogs = await auditLogService.GetRecentDeletedEventsAsync(managementSession, 20, CancellationToken.None);

            CalendarEventDeletionAuditLogItem auditLog = Assert.Single(auditLogs);
            Assert.Equal(ECalendarEventDeletionAuditStatus.Failed, auditLog.Status);
            Assert.Equal("Google Calendar deletion failed.", auditLog.ErrorMessage);
            Assert.Equal("planning-calendar", auditLog.CalendarID);
            Assert.Equal(new DateTimeOffset(2026, 5, 23, 9, 0, 0, TimeSpan.FromHours(9)), auditLog.EventStart);
            Assert.Equal(new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.FromHours(9)), auditLog.EventEnd);
        }
    }

    private static UserSession createUserSession(EUserRole role)
    {
        return new UserSession(7, "planning-lead", "기획 책임자", role, 1, "Planning", "기획팀");
    }

    private static CalendarEventItem createCalendarEvent()
    {
        CalendarEventItem calendarEvent = new CalendarEventItem();
        calendarEvent.ID = "organization-event-1";
        calendarEvent.CalendarID = "planning-calendar";
        calendarEvent.CalendarName = "기획팀";
        calendarEvent.Summary = "기획팀 리허설";
        calendarEvent.Location = "강당";
        calendarEvent.Start = new DateTimeOffset(2026, 5, 23, 9, 0, 0, TimeSpan.FromHours(9));
        calendarEvent.End = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.FromHours(9));
        calendarEvent.IsAllDay = false;
        return calendarEvent;
    }
}
