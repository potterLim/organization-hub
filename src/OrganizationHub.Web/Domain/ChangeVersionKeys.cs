namespace OrganizationHub.Web.Domain;

public static class ChangeVersionKeys
{
    public static readonly ChangeVersionKey DRIVE_FILES = new ChangeVersionKey("DriveFiles");
    public static readonly ChangeVersionKey CALENDAR = new ChangeVersionKey("Calendar");
    public static readonly ChangeVersionKey TASKS = new ChangeVersionKey("Todo");
    public static readonly ChangeVersionKey APPROVAL = new ChangeVersionKey("Approval");

    public static ChangeVersionKey CreateDriveFolderKey(DriveFolderId folderId)
    {
        return new ChangeVersionKey("DriveFolder:" + normalizeKeyPart(folderId.GetOptionalValue()));
    }

    public static ChangeVersionKey CreateCalendarKey(CalendarId calendarId)
    {
        return new ChangeVersionKey("Calendar:" + normalizeKeyPart(calendarId.GetOptionalValue()));
    }

    public static ChangeVersionKey CreateTodoTaskListKey(TodoTaskListId taskListId)
    {
        return new ChangeVersionKey("TodoTaskList:" + normalizeKeyPart(taskListId.GetOptionalValue()));
    }

    private static string normalizeKeyPart(string keyPart)
    {
        if (string.IsNullOrWhiteSpace(keyPart))
        {
            return "none";
        }

        return keyPart.Trim();
    }
}
