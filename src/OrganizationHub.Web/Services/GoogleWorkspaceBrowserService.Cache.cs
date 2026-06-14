using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceBrowserService
{
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

    private string createDriveFolderCacheKey(DriveFolderId parentFolderId)
    {
        return "OrganizationHub:GoogleWorkspace:DriveFolders:" + parentFolderId.GetRequiredValue();
    }

    private string createCalendarListCacheKey()
    {
        return "OrganizationHub:GoogleWorkspace:Calendars";
    }

    private string createTaskListCacheKey()
    {
        return "OrganizationHub:GoogleWorkspace:TaskLists";
    }
}
