using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGoogleWorkspaceSetupService
{
    Task<IReadOnlyList<GoogleFolderAssignmentOption>> GetFolderAssignmentOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleCalendarAssignmentOption>> GetCalendarAssignmentOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleTaskListAssignmentOption>> GetTaskListAssignmentOptionsAsync(CancellationToken cancellationToken);
    Task<ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>> EnsureStandardDriveFoldersAsync(CancellationToken cancellationToken);
    Task<ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>> EnsureStandardCalendarsAsync(CancellationToken cancellationToken);
    Task<ServiceResultOfT<GoogleDriveFolderNameSyncSummary>> ApplyStandardDriveFolderNamesAsync(CancellationToken cancellationToken);
    Task<ServiceResultOfT<GoogleTaskListSetupSummary>> EnsureStandardTaskListsAsync(CancellationToken cancellationToken);
    Task<ServiceResult> AssignFolderAsync(GoogleWorkspaceAssignmentTargetKey targetKey, DriveFolderId folderId, CancellationToken cancellationToken);
    Task<ServiceResult> AssignCalendarAsync(GoogleWorkspaceAssignmentTargetKey targetKey, CalendarId calendarId, CancellationToken cancellationToken);
    Task<ServiceResult> AssignTaskListAsync(GoogleWorkspaceAssignmentTargetKey targetKey, TodoTaskListId taskListId, CancellationToken cancellationToken);
}
