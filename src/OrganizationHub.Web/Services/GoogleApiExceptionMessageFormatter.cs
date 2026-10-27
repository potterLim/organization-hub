using System;

namespace OrganizationHub.Web.Services;

public static class GoogleApiExceptionMessageFormatter
{
    public static string CreateGoogleDriveMessage(Exception exception, string defaultPrefix)
    {
        string? reconnectMessageOrNull = createReconnectMessageOrNull(exception);
        if (reconnectMessageOrNull != null)
        {
            return reconnectMessageOrNull;
        }

        string message = exception.Message;
        if (message.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Drive 권한이 필요합니다. 다시 연결을 눌러 권한을 새로 승인해 주세요.";
        }

        if (message.Contains("drive.googleapis.com", StringComparison.OrdinalIgnoreCase) && (message.Contains("has not been used", StringComparison.OrdinalIgnoreCase) || message.Contains("disabled", StringComparison.OrdinalIgnoreCase)))
        {
            return "Google Cloud 프로젝트에서 Google Drive API가 꺼져 있습니다. Google Cloud Console에서 Google Drive API를 사용 설정한 뒤 다시 시도해 주세요.";
        }

        return defaultPrefix;
    }

    public static string CreateGoogleCalendarMessage(Exception exception, string defaultPrefix)
    {
        string? reconnectMessageOrNull = createReconnectMessageOrNull(exception);
        if (reconnectMessageOrNull != null)
        {
            return reconnectMessageOrNull;
        }

        string message = exception.Message;
        if (message.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Calendar 권한이 필요합니다. 다시 연결을 눌러 권한을 새로 승인해 주세요.";
        }

        if (message.Contains("calendar.googleapis.com", StringComparison.OrdinalIgnoreCase) && (message.Contains("has not been used", StringComparison.OrdinalIgnoreCase) || message.Contains("disabled", StringComparison.OrdinalIgnoreCase)))
        {
            return "Google Cloud 프로젝트에서 Google Calendar API가 꺼져 있습니다. Google Cloud Console에서 Google Calendar API를 사용 설정한 뒤 다시 시도해 주세요.";
        }

        return defaultPrefix;
    }

    public static string CreateGoogleTasksMessage(Exception exception, string defaultPrefix)
    {
        string? reconnectMessageOrNull = createReconnectMessageOrNull(exception);
        if (reconnectMessageOrNull != null)
        {
            return reconnectMessageOrNull;
        }

        string message = exception.Message;
        if (message.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Tasks 권한이 필요합니다. 다시 연결을 눌러 권한을 새로 승인해 주세요.";
        }

        if (message.Contains("tasks.googleapis.com", StringComparison.OrdinalIgnoreCase) && (message.Contains("has not been used", StringComparison.OrdinalIgnoreCase) || message.Contains("disabled", StringComparison.OrdinalIgnoreCase)))
        {
            return "Google Cloud 프로젝트에서 Google Tasks API가 꺼져 있습니다. Google Cloud Console에서 Google Tasks API를 사용 설정한 뒤 다시 시도해 주세요.";
        }

        return defaultPrefix;
    }

    private static string? createReconnectMessageOrNull(Exception exception)
    {
        string message = exception.Message;
        if (message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("expired or revoked", StringComparison.OrdinalIgnoreCase))
        {
            return "Google 연결 권한이 만료되었거나 회수되었습니다. 다시 연결을 눌러 권한을 새로 승인해 주세요.";
        }

        return null;
    }
}
