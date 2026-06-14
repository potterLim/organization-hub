using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IGoogleWorkspaceBrowserService
{
    Task<IReadOnlyList<GoogleDriveFolderItem>> ListDriveFoldersAsync(DriveFolderId parentFolderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleCalendarListItem>> ListCalendarsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleTaskListItem>> ListTaskListsAsync(CancellationToken cancellationToken);
}
