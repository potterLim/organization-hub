using System;
using System.Globalization;

namespace OrganizationHub.Web.Services;

internal static class PresentationDateTimeFormatter
{
    internal const string DATE_FORMAT = "yyyy-MM-dd";
    internal const string FULL_DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm";
    internal const string HOUR_MINUTE_FORMAT = "HH:mm";
    internal const string MONTH_DAY_TIME_FORMAT = "MM-dd HH:mm";

    internal static string formatClientDateTime(DateTime dateTime, TimeZoneInfo clientTimeZoneInfo, string format)
    {
        DateTime clientDateTime = ClientTimeZoneUtility.createClientDateTime(dateTime, clientTimeZoneInfo);
        return clientDateTime.ToString(format, CultureInfo.InvariantCulture);
    }

    internal static string formatClientDateTime(DateTimeOffset dateTime, TimeZoneInfo clientTimeZoneInfo, string format)
    {
        DateTime clientDateTime = ClientTimeZoneUtility.createClientDateTime(dateTime.UtcDateTime, clientTimeZoneInfo);
        return clientDateTime.ToString(format, CultureInfo.InvariantCulture);
    }

    internal static string formatOptionalClientDateTime(DateTime? dateTimeOrNull, TimeZoneInfo clientTimeZoneInfo, string format)
    {
        if (dateTimeOrNull.HasValue == false)
        {
            return "-";
        }

        return formatClientDateTime(dateTimeOrNull.Value, clientTimeZoneInfo, format);
    }
}
