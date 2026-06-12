using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGoogleWorkspaceSetupService
{
    Task<IReadOnlyList<GoogleFolderAssignmentOption>> GetFolderAssignmentOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleCalendarAssignmentOption>> GetCalendarAssignmentOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleTaskListAssignmentOption>> GetTaskListAssignmentOptionsAsync(CancellationToken cancellationToken);
    Task<ServiceResultOfT<GoogleDriveFolderNameSyncSummary>> ApplyStandardDriveFolderNamesAsync(CancellationToken cancellationToken);
    Task<ServiceResultOfT<GoogleTaskListSetupSummary>> EnsureStandardTaskListsAsync(CancellationToken cancellationToken);
    Task<ServiceResult> AssignFolderAsync(string targetKey, string folderID, CancellationToken cancellationToken);
    Task<ServiceResult> AssignCalendarAsync(string targetKey, string calendarID, CancellationToken cancellationToken);
    Task<ServiceResult> AssignTaskListAsync(string targetKey, string taskListID, CancellationToken cancellationToken);
}
