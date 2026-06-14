using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct CalendarId
{
    public const string ALL_CALENDARS_VALUE = "all";

    public string Value
    {
        get;
    }

    public bool IsAggregate
    {
        get
        {
            return Value == ALL_CALENDARS_VALUE;
        }
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static CalendarId Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static CalendarId AllCalendars
    {
        get
        {
            return CreateRequired(ALL_CALENDARS_VALUE);
        }
    }

    public static CalendarId CreateOptional(string? calendarIdOrNull)
    {
        return new CalendarId(calendarIdOrNull);
    }

    public static CalendarId CreateRequired(string? calendarIdOrNull)
    {
        CalendarId calendarId = CreateOptional(calendarIdOrNull);
        if (calendarId.HasValue == false)
        {
            throw new ArgumentException("Calendar ID is required.", nameof(calendarIdOrNull));
        }

        return calendarId;
    }

    private CalendarId(string? calendarIdOrNull)
    {
        Value = string.IsNullOrWhiteSpace(calendarIdOrNull) ? string.Empty : calendarIdOrNull.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Calendar ID is required.");
        }

        return Value;
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
