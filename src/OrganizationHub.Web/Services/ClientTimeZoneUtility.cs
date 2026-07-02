using System;

namespace OrganizationHub.Web.Services;

internal static class ClientTimeZoneUtility
{
    internal static TimeZoneInfo resolveTimeZone(string? timeZoneIdOrNull)
    {
        if (string.IsNullOrWhiteSpace(timeZoneIdOrNull))
        {
            return TimeZoneInfo.Local;
        }

        string timeZoneId = timeZoneIdOrNull.Trim();
        TimeZoneInfo? timeZoneInfoOrNull = tryFindSystemTimeZoneOrNull(timeZoneId);
        if (timeZoneInfoOrNull != null)
        {
            return timeZoneInfoOrNull;
        }

        string? windowsTimeZoneIdOrNull;
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out windowsTimeZoneIdOrNull) && string.IsNullOrWhiteSpace(windowsTimeZoneIdOrNull) == false)
        {
            timeZoneInfoOrNull = tryFindSystemTimeZoneOrNull(windowsTimeZoneIdOrNull);
            if (timeZoneInfoOrNull != null)
            {
                return timeZoneInfoOrNull;
            }
        }

        string? ianaTimeZoneIdOrNull;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out ianaTimeZoneIdOrNull) && string.IsNullOrWhiteSpace(ianaTimeZoneIdOrNull) == false)
        {
            timeZoneInfoOrNull = tryFindSystemTimeZoneOrNull(ianaTimeZoneIdOrNull);
            if (timeZoneInfoOrNull != null)
            {
                return timeZoneInfoOrNull;
            }
        }

        return TimeZoneInfo.Local;
    }

    internal static DateTime createUtcDateTime(DateTime localDateTime, TimeZoneInfo clientTimeZoneInfo)
    {
        DateTime unspecifiedLocalDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        TimeSpan utcOffset = clientTimeZoneInfo.GetUtcOffset(unspecifiedLocalDateTime);
        DateTimeOffset clientDateTime = new DateTimeOffset(unspecifiedLocalDateTime, utcOffset);
        return clientDateTime.UtcDateTime;
    }

    internal static DateTime createClientDateTime(DateTime utcDateTime, TimeZoneInfo clientTimeZoneInfo)
    {
        DateTime normalizedUtcDateTime = normalizeUtcDateTime(utcDateTime);
        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtcDateTime, clientTimeZoneInfo);
    }

    internal static DateTime getClientNow(TimeZoneInfo clientTimeZoneInfo)
    {
        return createClientDateTime(DateTime.UtcNow, clientTimeZoneInfo);
    }

    internal static DateTime normalizeUtcDateTimeValue(DateTime utcDateTime)
    {
        return normalizeUtcDateTime(utcDateTime);
    }

    private static DateTime normalizeUtcDateTime(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
        {
            return dateTime;
        }

        if (dateTime.Kind == DateTimeKind.Local)
        {
            return dateTime.ToUniversalTime();
        }

        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    private static TimeZoneInfo? tryFindSystemTimeZoneOrNull(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
