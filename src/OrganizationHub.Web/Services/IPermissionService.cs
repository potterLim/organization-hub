using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IPermissionService
{
    Task<IReadOnlyList<DriveFolderReference>> GetReadableDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFolderReference>> GetReadableDriveFoldersAsync(UserSession userSession, EDriveFolderKind folderKind, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFolderReference>> GetUploadableDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFolderReference>> GetApprovalDestinationDriveFoldersAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarReference>> GetReadableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarReference>> GetWritableCalendarsAsync(UserSession userSession, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideReadFileAsync(UserSession userSession, string fileID, CancellationToken cancellationToken);
    Task<bool> CanReadFileAsync(UserSession userSession, string fileID, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideUploadToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken);
    Task<bool> CanUploadToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideCreateFolderInFolderAsync(UserSession userSession, string parentFolderID, CancellationToken cancellationToken);
    Task<bool> CanCreateFolderInFolderAsync(UserSession userSession, string parentFolderID, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideDeleteInFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken);
    Task<bool> CanDeleteInFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken);
    PermissionDecision DecideDeleteInsideRoot(UserSession userSession, DriveFolderReference rootFolder);
    bool CanDeleteInsideRoot(UserSession userSession, DriveFolderReference rootFolder);
    Task<PermissionDecision> DecideStoreApprovedFileToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken);
    Task<bool> CanStoreApprovedFileToFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken);
    Task<bool> CanReadCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken);
    Task<bool> CanWriteCalendarAsync(UserSession userSession, string calendarID, CancellationToken cancellationToken);
}
