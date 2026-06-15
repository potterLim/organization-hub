using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct CalendarEventId
{
    private string Value
    {
        get;
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static CalendarEventId Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static CalendarEventId CreateOptional(string? eventIdOrNull)
    {
        return new CalendarEventId(eventIdOrNull);
    }

    public static CalendarEventId CreateRequired(string? eventIdOrNull)
    {
        CalendarEventId eventId = CreateOptional(eventIdOrNull);
        if (eventId.HasValue == false)
        {
            throw new ArgumentException("Calendar event ID is required.", nameof(eventIdOrNull));
        }

        return eventId;
    }

    private CalendarEventId(string? eventIdOrNull)
    {
        Value = string.IsNullOrWhiteSpace(eventIdOrNull) ? string.Empty : eventIdOrNull.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Calendar event ID is required.");
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
