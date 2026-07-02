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
    Task<PermissionDecision> DecideReadFileAsync(UserSession userSession, DriveItemId fileId, CancellationToken cancellationToken);
    Task<bool> CanReadFileAsync(UserSession userSession, DriveItemId fileId, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideUploadToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken);
    Task<bool> CanUploadToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideCreateFolderInFolderAsync(UserSession userSession, DriveFolderId parentFolderId, CancellationToken cancellationToken);
    Task<bool> CanCreateFolderInFolderAsync(UserSession userSession, DriveFolderId parentFolderId, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideDeleteInFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken);
    Task<bool> CanDeleteInFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideStoreApprovedFileToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken);
    Task<bool> CanStoreApprovedFileToFolderAsync(UserSession userSession, DriveFolderId folderId, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideReadCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken);
    Task<bool> CanReadCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken);
    Task<PermissionDecision> DecideWriteCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken);
    Task<bool> CanWriteCalendarAsync(UserSession userSession, CalendarId calendarId, CancellationToken cancellationToken);
}
