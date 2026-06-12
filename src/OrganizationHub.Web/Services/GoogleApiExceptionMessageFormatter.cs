using System;

namespace OrganizationHub.Web.Services;

public static class GoogleApiExceptionMessageFormatter
{
    public static string CreateGoogleCalendarMessage(Exception exception, string defaultPrefix)
    {
        string message = exception.Message;
        if (message.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Calendar 권한이 필요합니다. 연결 해제 후 계정 연결을 다시 진행해 주세요.";
        }

        if (message.Contains("calendar.googleapis.com", StringComparison.OrdinalIgnoreCase) && (message.Contains("has not been used", StringComparison.OrdinalIgnoreCase) || message.Contains("disabled", StringComparison.OrdinalIgnoreCase)))
        {
            return "Google Cloud 프로젝트에서 Google Calendar API가 꺼져 있습니다. Google Cloud Console에서 Google Calendar API를 사용 설정한 뒤 다시 시도해 주세요.";
        }

        return defaultPrefix + " " + message;
    }

    public static string CreateGoogleTasksMessage(Exception exception, string defaultPrefix)
    {
        string message = exception.Message;
        if (message.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Tasks 권한이 필요합니다. 연결 해제 후 계정 연결을 다시 진행해 주세요.";
        }

        if (message.Contains("tasks.googleapis.com", StringComparison.OrdinalIgnoreCase) && (message.Contains("has not been used", StringComparison.OrdinalIgnoreCase) || message.Contains("disabled", StringComparison.OrdinalIgnoreCase)))
        {
            return "Google Cloud 프로젝트에서 Google Tasks API가 꺼져 있습니다. Google Cloud Console에서 Google Tasks API를 사용 설정한 뒤 다시 시도해 주세요.";
        }

        return defaultPrefix + " " + message;
    }
}
