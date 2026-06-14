using System.Collections.Generic;
using OrganizationHub.Web.Models;
using CalendarListData = Google.Apis.Calendar.v3.Data.CalendarList;
using CalendarListEntryData = Google.Apis.Calendar.v3.Data.CalendarListEntry;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;
using TaskListData = Google.Apis.Tasks.v1.Data.TaskList;
using TaskListsData = Google.Apis.Tasks.v1.Data.TaskLists;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceBrowserService
{
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
