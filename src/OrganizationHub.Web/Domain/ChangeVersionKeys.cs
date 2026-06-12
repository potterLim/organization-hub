namespace OrganizationHub.Web.Domain;

public static class ChangeVersionKeys
{
    public const string DRIVE_FILES = "DriveFiles";
    public const string CALENDAR = "Calendar";
    public const string TODO = "Todo";
    public const string APPROVAL = "Approval";

    public static string CreateDriveFolderKey(string folderID)
    {
        return "DriveFolder:" + normalizeKeyPart(folderID);
    }

    public static string CreateCalendarKey(string calendarID)
    {
        return "Calendar:" + normalizeKeyPart(calendarID);
    }

    public static string CreateTodoTaskListKey(string taskListID)
    {
        return "TodoTaskList:" + normalizeKeyPart(taskListID);
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
