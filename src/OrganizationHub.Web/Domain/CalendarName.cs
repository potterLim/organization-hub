namespace OrganizationHub.Web.Domain;

public readonly record struct CalendarName
{
    private readonly string? mValueOrNull;

    private string Value
    {
        get
        {
            if (mValueOrNull == null)
            {
                return string.Empty;
            }

            return mValueOrNull;
        }
    }

    public static CalendarName Empty
    {
        get
        {
            return new CalendarName(string.Empty);
        }
    }

    private CalendarName(string calendarName)
    {
        mValueOrNull = calendarName;
    }

    public static CalendarName CreateOptional(string? calendarNameOrNull)
    {
        if (string.IsNullOrWhiteSpace(calendarNameOrNull))
        {
            return Empty;
        }

        return new CalendarName(calendarNameOrNull.Trim());
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
