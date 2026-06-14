namespace OrganizationHub.Web.Domain;

public static class ChangeVersionKeys
{
    public const string DRIVE_FILES = "DriveFiles";
    public const string CALENDAR = "Calendar";
    public const string TODO = "Todo";
    public const string APPROVAL = "Approval";

    public static string CreateDriveFolderKey(DriveFolderID folderID)
    {
        return "DriveFolder:" + normalizeKeyPart(folderID.Value);
    }

    public static string CreateCalendarKey(CalendarID calendarID)
    {
        return "Calendar:" + normalizeKeyPart(calendarID.Value);
    }

    public static string CreateTodoTaskListKey(TodoTaskListID taskListID)
    {
        return "TodoTaskList:" + normalizeKeyPart(taskListID.Value);
    }

    private static string normalizeKeyPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        return value.Trim();
    }
}
