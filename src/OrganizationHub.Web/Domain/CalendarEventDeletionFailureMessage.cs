namespace OrganizationHub.Web.Domain;

public readonly record struct CalendarEventDeletionFailureMessage
{
    public const int MAX_LENGTH = 1024;

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

    public static CalendarEventDeletionFailureMessage Empty
    {
        get
        {
            return new CalendarEventDeletionFailureMessage(string.Empty);
        }
    }

    private CalendarEventDeletionFailureMessage(string failureMessage)
    {
        mValueOrNull = failureMessage;
    }

    public static CalendarEventDeletionFailureMessage CreateOptional(string? failureMessageOrNull)
    {
        if (string.IsNullOrWhiteSpace(failureMessageOrNull))
        {
            return Empty;
        }

        string failureMessage = failureMessageOrNull.Trim();
        if (failureMessage.Length > MAX_LENGTH)
        {
            failureMessage = failureMessage.Substring(0, MAX_LENGTH);
        }

        return new CalendarEventDeletionFailureMessage(failureMessage);
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
