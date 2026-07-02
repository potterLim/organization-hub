using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct TodoDueAtUtc
{
    private readonly DateTime? mValueOrNull;

    public bool HasValue
    {
        get
        {
            return mValueOrNull.HasValue;
        }
    }

    public static TodoDueAtUtc Empty
    {
        get
        {
            return new TodoDueAtUtc(null);
        }
    }

    private TodoDueAtUtc(DateTime? dueAtUtcOrNull)
    {
        mValueOrNull = dueAtUtcOrNull;
    }

    public static TodoDueAtUtc CreateOptionalUtc(DateTime? dueAtUtcOrNull)
    {
        if (dueAtUtcOrNull.HasValue == false)
        {
            return Empty;
        }

        return CreateRequiredUtc(dueAtUtcOrNull.Value);
    }

    public static TodoDueAtUtc CreateRequiredUtc(DateTime dueAtUtc)
    {
        ensureUtcDateTime(dueAtUtc);
        return new TodoDueAtUtc(dueAtUtc);
    }

    public static TodoDueAtUtc CreateOptionalFromStoredValue(DateTime? dueAtOrNull)
    {
        if (dueAtOrNull.HasValue == false)
        {
            return Empty;
        }

        return CreateRequiredFromStoredValue(dueAtOrNull.Value);
    }

    public static TodoDueAtUtc CreateRequiredFromStoredValue(DateTime dueAt)
    {
        DateTime normalizedDueAtUtc = normalizeUtcDateTime(dueAt);
        return new TodoDueAtUtc(normalizedDueAtUtc);
    }

    public DateTime? GetOptionalValueOrNull()
    {
        return mValueOrNull;
    }

    public DateTime GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Todo due date is required.");
        }

        return mValueOrNull.GetValueOrDefault();
    }

    private static void ensureUtcDateTime(DateTime dueAtUtc)
    {
        if (dueAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Todo due date must be UTC.", nameof(dueAtUtc));
        }
    }

    private static DateTime normalizeUtcDateTime(DateTime dueAt)
    {
        if (dueAt.Kind == DateTimeKind.Utc)
        {
            return dueAt;
        }

        if (dueAt.Kind == DateTimeKind.Local)
        {
            return dueAt.ToUniversalTime();
        }

        return DateTime.SpecifyKind(dueAt, DateTimeKind.Utc);
    }
}
