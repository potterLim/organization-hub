using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceBrowserService
{
    private bool tryGetCachedDriveFolders(string cacheKey, out IReadOnlyList<GoogleDriveFolderItem> cachedItems)
    {
        IReadOnlyList<GoogleDriveFolderItem>? cachedFoldersOrNull = mMemoryCache.Get<IReadOnlyList<GoogleDriveFolderItem>>(cacheKey);
        if (cachedFoldersOrNull == null)
        {
            cachedItems = System.Array.Empty<GoogleDriveFolderItem>();
            return false;
        }

        cachedItems = cloneDriveFolders(cachedFoldersOrNull);
        return true;
    }

    private void setDriveFolderCache(string cacheKey, IReadOnlyList<GoogleDriveFolderItem> folders)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = DRIVE_FOLDER_CACHE_DURATION;
        mMemoryCache.Set(cacheKey, cloneDriveFolders(folders), cacheEntryOptions);
    }

    private bool tryGetCachedCalendars(out IReadOnlyList<GoogleCalendarListItem> cachedItems)
    {
        IReadOnlyList<GoogleCalendarListItem>? cachedCalendarsOrNull = mMemoryCache.Get<IReadOnlyList<GoogleCalendarListItem>>(createCalendarListCacheKey());
        if (cachedCalendarsOrNull == null)
        {
            cachedItems = System.Array.Empty<GoogleCalendarListItem>();
            return false;
        }

        cachedItems = cloneCalendars(cachedCalendarsOrNull);
        return true;
    }

    private void setCalendarCache(IReadOnlyList<GoogleCalendarListItem> calendars)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = CALENDAR_LIST_CACHE_DURATION;
        mMemoryCache.Set(createCalendarListCacheKey(), cloneCalendars(calendars), cacheEntryOptions);
    }

    private bool tryGetCachedTaskLists(out IReadOnlyList<GoogleTaskListItem> cachedItems)
    {
        IReadOnlyList<GoogleTaskListItem>? cachedTaskListsOrNull = mMemoryCache.Get<IReadOnlyList<GoogleTaskListItem>>(createTaskListCacheKey());
        if (cachedTaskListsOrNull == null)
        {
            cachedItems = System.Array.Empty<GoogleTaskListItem>();
            return false;
        }

        cachedItems = cloneTaskLists(cachedTaskListsOrNull);
        return true;
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
