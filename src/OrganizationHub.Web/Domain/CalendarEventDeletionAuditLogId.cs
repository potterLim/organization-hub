using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct CalendarEventDeletionAuditLogId
{
    public int Value
    {
        get;
    }

    public bool HasValue
    {
        get
        {
            return Value > 0;
        }
    }

    public static CalendarEventDeletionAuditLogId Empty
    {
        get
        {
            return CreateOptional(0);
        }
    }

    public static CalendarEventDeletionAuditLogId CreateOptional(int idValue)
    {
        if (idValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Calendar event deletion audit log ID cannot be negative.");
        }

        return new CalendarEventDeletionAuditLogId(idValue);
    }

    public static CalendarEventDeletionAuditLogId CreateExisting(int idValue)
    {
        if (idValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idValue), "Calendar event deletion audit log ID must be greater than zero.");
        }

        return new CalendarEventDeletionAuditLogId(idValue);
    }

    private CalendarEventDeletionAuditLogId(int idValue)
    {
        Value = idValue;
    }

    public int GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Calendar event deletion audit log ID is required.");
        }

        return Value;
    }
}
