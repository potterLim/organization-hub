using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Drive.v3;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using CalendarListData = Google.Apis.Calendar.v3.Data.CalendarList;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;
using TaskListsData = Google.Apis.Tasks.v1.Data.TaskLists;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceBrowserService : IGoogleWorkspaceBrowserService
{
    private static readonly string[] DRIVE_SCOPES = new string[] { DriveService.Scope.Drive };
    private static readonly string[] CALENDAR_SCOPES = new string[] { CalendarService.Scope.Calendar };
    private static readonly string[] TASK_SCOPES = new string[] { GoogleTasksApi.Scope.Tasks };
    private static readonly TimeSpan DRIVE_FOLDER_CACHE_DURATION = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CALENDAR_LIST_CACHE_DURATION = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TASK_LIST_CACHE_DURATION = TimeSpan.FromSeconds(60);

    private readonly IGoogleApiCredentialFactory mCredentialFactory;
    private readonly GoogleApiOptions mOptions;
    private readonly IMemoryCache mMemoryCache;

    public GoogleWorkspaceBrowserService(IGoogleApiCredentialFactory credentialFactory, IOptions<GoogleApiOptions> options, IMemoryCache memoryCache)
    {
        mCredentialFactory = credentialFactory;
        mOptions = options.Value;
        mMemoryCache = memoryCache;
    }

    public async Task<IReadOnlyList<GoogleDriveFolderItem>> ListDriveFoldersAsync(DriveFolderId parentFolderId, CancellationToken cancellationToken)
    {
        DriveFolderId effectiveParentFolderId = getEffectiveParentFolderId(parentFolderId);
        string cacheKey = createDriveFolderCacheKey(effectiveParentFolderId);
        IReadOnlyList<GoogleDriveFolderItem>? cachedFoldersOrNull = getCachedDriveFoldersOrNull(cacheKey);
        if (cachedFoldersOrNull != null)
        {
            return cachedFoldersOrNull;
        }

        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        FilesResource.ListRequest request = driveService.Files.List();
        request.Q = "'" + escapeDriveQueryValue(effectiveParentFolderId.GetRequiredValue()) + "' in parents and mimeType = '" + GoogleMimeTypes.DRIVE_FOLDER + "' and trashed = false";
        request.Fields = "files(id,name,modifiedTime)";
        request.OrderBy = "name";
        request.SupportsAllDrives = true;
        request.IncludeItemsFromAllDrives = true;
        DriveFileList fileList = await request.ExecuteAsync(cancellationToken);
        IReadOnlyList<GoogleDriveFolderItem> folders = createFolderItems(fileList);
        setDriveFolderCache(cacheKey, folders);
        return cloneDriveFolders(folders);
    }

    public async Task<IReadOnlyList<GoogleCalendarListItem>> ListCalendarsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleCalendarListItem>? cachedCalendarsOrNull = getCachedCalendarsOrNull();
        if (cachedCalendarsOrNull != null)
        {
            return cachedCalendarsOrNull;
        }

        CalendarService calendarService = await createCalendarServiceAsync(cancellationToken);
        CalendarListResource.ListRequest request = calendarService.CalendarList.List();
        request.Fields = "items(id,summary,accessRole,primary)";
        CalendarListData calendarList = await request.ExecuteAsync(cancellationToken);
        IReadOnlyList<GoogleCalendarListItem> calendars = createCalendarItems(calendarList);
        setCalendarCache(calendars);
        return cloneCalendars(calendars);
    }

    public async Task<IReadOnlyList<GoogleTaskListItem>> ListTaskListsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleTaskListItem>? cachedTaskListsOrNull = getCachedTaskListsOrNull();
        if (cachedTaskListsOrNull != null)
        {
            return cachedTaskListsOrNull;
        }

        GoogleTasksApi tasksService = await createTasksServiceAsync(cancellationToken);
        Google.Apis.Tasks.v1.TasklistsResource.ListRequest request = tasksService.Tasklists.List();
        request.Fields = "items(id,title)";
        TaskListsData taskLists = await request.ExecuteAsync(cancellationToken);
        IReadOnlyList<GoogleTaskListItem> listItems = createTaskListItems(taskLists);
        setTaskListCache(listItems);
        return cloneTaskLists(listItems);
    }

    private DriveFolderId getEffectiveParentFolderId(DriveFolderId parentFolderId)
    {
        if (parentFolderId.HasValue)
        {
            return parentFolderId;
        }

        return DriveFolderId.Root;
    }
}
