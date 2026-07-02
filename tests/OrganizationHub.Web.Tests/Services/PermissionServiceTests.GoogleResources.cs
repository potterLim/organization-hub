using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Fakes;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class PermissionServiceTests
{
    private static FakeDriveService createConfiguredDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("public-folder", "외부 배포자료", "official-root");
        driveService.AddFolder("common-folder", "공통 자료", "official-root");
        driveService.AddFolder("management-folder", "관리팀 자료", "official-root");
        driveService.AddFolder("management-approved-folder", "관리팀 결재 완료 자료", "official-root");
        driveService.AddFolder("responsible-folder", "책임자 자료", "official-root");
        driveService.AddFolder("work-resources-folder", "업무 지원 자료", "official-root");
        driveService.AddFolder("archive-folder", "Archived Materials", "official-root");
        driveService.AddFolder("planning-team-folder", "기획팀", "official-root");
        driveService.AddFolder("operation-team-folder", "운영팀", "official-root");
        driveService.AddFolder("planning-materials-folder", "기획팀 자료", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-approved-folder", "기획팀 결재 완료 자료", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-child-folder", "회의록", "planning-materials-folder", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-approved-child-folder", "예산", "planning-approved-folder", "planning-team-folder", "official-root");
        driveService.AddFolder("operation-materials-folder", "운영팀 자료", "operation-team-folder", "official-root");
        driveService.AddFolder("operation-approved-folder", "운영팀 결재 완료 자료", "operation-team-folder", "official-root");
        driveService.AddFolder("operation-child-folder", "회의록", "operation-materials-folder", "operation-team-folder", "official-root");
        driveService.AddFile("planning-materials-file", "2026-05-21_기획팀_회의록.pdf", "application/pdf", "planning-materials-folder", "planning-team-folder", "official-root");
        driveService.AddFile("operation-materials-file", "2026-05-21_운영팀_회의록.pdf", "application/pdf", "operation-materials-folder", "operation-team-folder", "official-root");
        driveService.AddFile("common-file", "공통양식.pdf", "application/pdf", "common-folder", "official-root");
        driveService.AddFile("responsible-file", "책임자공유.pdf", "application/pdf", "responsible-folder", "official-root");
        driveService.AddFile("outside-file", "외부자료.pdf", "application/pdf", "outside-folder");
        return driveService;
    }

    private static DriveItemId driveItemId(string value)
    {
        return DriveItemId.CreateOptional(value);
    }

    private static DriveFolderId driveFolderId(string value)
    {
        return DriveFolderId.CreateOptional(value);
    }

    private static CalendarId calendarId(string value)
    {
        return CalendarId.CreateOptional(value);
    }

    private static bool hasCalendar(IReadOnlyList<CalendarReference> calendars, string calendarId)
    {
        CalendarReference? calendarOrNull = calendars.FirstOrDefault(calendar => calendar.CalendarId.GetRequiredValue() == calendarId);
        return calendarOrNull != null;
    }

    private static string getCalendarDisplayName(IReadOnlyList<CalendarReference> calendars, string calendarId)
    {
        CalendarReference? calendarOrNull = calendars.FirstOrDefault(calendar => calendar.CalendarId.GetRequiredValue() == calendarId);
        if (calendarOrNull == null)
        {
            return string.Empty;
        }

        return calendarOrNull.DisplayName;
    }
}
