using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Calendar.v3;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Models;
using CalendarListData = Google.Apis.Calendar.v3.Data.CalendarList;
using CalendarListEntryData = Google.Apis.Calendar.v3.Data.CalendarListEntry;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;
using GoogleTasksApi = Google.Apis.Tasks.v1.TasksService;
using TaskListData = Google.Apis.Tasks.v1.Data.TaskList;
using TaskListsData = Google.Apis.Tasks.v1.Data.TaskLists;

namespace OrganizationHub.Web.Services;

public sealed class GoogleWorkspaceBrowserService : IGoogleWorkspaceBrowserService
{
    private static readonly string[] DRIVE_SCOPES = new[] { DriveService.Scope.Drive };
    private static readonly string[] CALENDAR_SCOPES = new[] { CalendarService.Scope.Calendar };
    private static readonly string[] TASK_SCOPES = new[] { GoogleTasksApi.Scope.Tasks };
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

    public async Task<IReadOnlyList<GoogleDriveFolderItem>> ListDriveFoldersAsync(string parentFolderID, CancellationToken cancellationToken)
    {
        string effectiveParentFolderID = string.IsNullOrWhiteSpace(parentFolderID) ? "root" : parentFolderID;
        string cacheKey = createDriveFolderCacheKey(effectiveParentFolderID);
        IReadOnlyList<GoogleDriveFolderItem>? cachedFoldersOrNull = getCachedDriveFoldersOrNull(cacheKey);
        if (cachedFoldersOrNull != null)
        {
            return cachedFoldersOrNull;
        }

        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        FilesResource.ListRequest request = driveService.Files.List();
        request.Q = "'" + escapeDriveQueryValue(effectiveParentFolderID) + "' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
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

    private async Task<DriveService> createDriveServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(DRIVE_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new DriveService(initializer);
    }

    private async Task<CalendarService> createCalendarServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(CALENDAR_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new CalendarService(initializer);
    }

    private async Task<GoogleTasksApi> createTasksServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(TASK_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new GoogleTasksApi(initializer);
    }

    private IReadOnlyList<GoogleDriveFolderItem>? getCachedDriveFoldersOrNull(string cacheKey)
    {
        IReadOnlyList<GoogleDriveFolderItem>? cachedFoldersOrNull = mMemoryCache.Get<IReadOnlyList<GoogleDriveFolderItem>>(cacheKey);
        if (cachedFoldersOrNull == null)
        {
            return null;
        }

        return cloneDriveFolders(cachedFoldersOrNull);
    }

    private void setDriveFolderCache(string cacheKey, IReadOnlyList<GoogleDriveFolderItem> folders)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = DRIVE_FOLDER_CACHE_DURATION;
        mMemoryCache.Set(cacheKey, cloneDriveFolders(folders), cacheEntryOptions);
    }

    private IReadOnlyList<GoogleCalendarListItem>? getCachedCalendarsOrNull()
    {
        IReadOnlyList<GoogleCalendarListItem>? cachedCalendarsOrNull = mMemoryCache.Get<IReadOnlyList<GoogleCalendarListItem>>(createCalendarListCacheKey());
        if (cachedCalendarsOrNull == null)
        {
            return null;
        }

        return cloneCalendars(cachedCalendarsOrNull);
    }

    private void setCalendarCache(IReadOnlyList<GoogleCalendarListItem> calendars)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = CALENDAR_LIST_CACHE_DURATION;
        mMemoryCache.Set(createCalendarListCacheKey(), cloneCalendars(calendars), cacheEntryOptions);
    }

    private IReadOnlyList<GoogleTaskListItem>? getCachedTaskListsOrNull()
    {
        IReadOnlyList<GoogleTaskListItem>? cachedTaskListsOrNull = mMemoryCache.Get<IReadOnlyList<GoogleTaskListItem>>(createTaskListCacheKey());
        if (cachedTaskListsOrNull == null)
        {
            return null;
        }

        return cloneTaskLists(cachedTaskListsOrNull);
    }

    private void setTaskListCache(IReadOnlyList<GoogleTaskListItem> taskLists)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = TASK_LIST_CACHE_DURATION;
        mMemoryCache.Set(createTaskListCacheKey(), cloneTaskLists(taskLists), cacheEntryOptions);
    }

    private string createDriveFolderCacheKey(string parentFolderID)
    {
        return "OrganizationHub:GoogleWorkspace:DriveFolders:" + parentFolderID;
    }

    private string createCalendarListCacheKey()
    {
        return "OrganizationHub:GoogleWorkspace:Calendars";
    }

    private string createTaskListCacheKey()
    {
        return "OrganizationHub:GoogleWorkspace:TaskLists";
    }

    private IReadOnlyList<GoogleDriveFolderItem> createFolderItems(DriveFileList fileList)
    {
        List<GoogleDriveFolderItem> folders = new List<GoogleDriveFolderItem>();
        if (fileList.Files == null)
        {
            return folders;
        }

        foreach (DriveDataFile file in fileList.Files)
        {
            GoogleDriveFolderItem folderItem = new GoogleDriveFolderItem();
            folderItem.ID = getNullAsEmpty(file.Id);
            folderItem.Name = getNullAsEmpty(file.Name);
            folderItem.ModifiedTime = file.ModifiedTimeDateTimeOffset.HasValue ? file.ModifiedTimeDateTimeOffset.Value.DateTime : null;
            folders.Add(folderItem);
        }

        return folders;
    }

    private IReadOnlyList<GoogleDriveFolderItem> cloneDriveFolders(IReadOnlyList<GoogleDriveFolderItem> folders)
    {
        List<GoogleDriveFolderItem> clonedFolders = new List<GoogleDriveFolderItem>();
        foreach (GoogleDriveFolderItem folder in folders)
        {
            GoogleDriveFolderItem clonedFolder = new GoogleDriveFolderItem();
            clonedFolder.ID = folder.ID;
            clonedFolder.Name = folder.Name;
            clonedFolder.ModifiedTime = folder.ModifiedTime;
            clonedFolders.Add(clonedFolder);
        }

        return clonedFolders;
    }

    private IReadOnlyList<GoogleCalendarListItem> createCalendarItems(CalendarListData calendarList)
    {
        List<GoogleCalendarListItem> calendars = new List<GoogleCalendarListItem>();
        if (calendarList.Items == null)
        {
            return calendars;
        }

        foreach (CalendarListEntryData calendarEntry in calendarList.Items)
        {
            GoogleCalendarListItem calendarItem = new GoogleCalendarListItem();
            calendarItem.ID = getNullAsEmpty(calendarEntry.Id);
            calendarItem.Summary = getNullAsEmpty(calendarEntry.Summary);
            calendarItem.AccessRole = getNullAsEmpty(calendarEntry.AccessRole);
            calendarItem.IsPrimary = calendarEntry.Primary.HasValue && calendarEntry.Primary.Value;
            calendars.Add(calendarItem);
        }

        return calendars;
    }

    private IReadOnlyList<GoogleCalendarListItem> cloneCalendars(IReadOnlyList<GoogleCalendarListItem> calendars)
    {
        List<GoogleCalendarListItem> clonedCalendars = new List<GoogleCalendarListItem>();
        foreach (GoogleCalendarListItem calendar in calendars)
        {
            GoogleCalendarListItem clonedCalendar = new GoogleCalendarListItem();
            clonedCalendar.ID = calendar.ID;
            clonedCalendar.Summary = calendar.Summary;
            clonedCalendar.AccessRole = calendar.AccessRole;
            clonedCalendar.IsPrimary = calendar.IsPrimary;
            clonedCalendars.Add(clonedCalendar);
        }

        return clonedCalendars;
    }

    private IReadOnlyList<GoogleTaskListItem> createTaskListItems(TaskListsData taskLists)
    {
        List<GoogleTaskListItem> listItems = new List<GoogleTaskListItem>();
        if (taskLists.Items == null)
        {
            return listItems;
        }

        foreach (TaskListData taskList in taskLists.Items)
        {
            GoogleTaskListItem listItem = new GoogleTaskListItem();
            listItem.ID = getNullAsEmpty(taskList.Id);
            listItem.Title = getNullAsEmpty(taskList.Title);
            listItems.Add(listItem);
        }

        return listItems;
    }

    private IReadOnlyList<GoogleTaskListItem> cloneTaskLists(IReadOnlyList<GoogleTaskListItem> taskLists)
    {
        List<GoogleTaskListItem> clonedTaskLists = new List<GoogleTaskListItem>();
        foreach (GoogleTaskListItem taskList in taskLists)
        {
            GoogleTaskListItem clonedTaskList = new GoogleTaskListItem();
            clonedTaskList.ID = taskList.ID;
            clonedTaskList.Title = taskList.Title;
            clonedTaskLists.Add(clonedTaskList);
        }

        return clonedTaskLists;
    }

    private string escapeDriveQueryValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }
}
