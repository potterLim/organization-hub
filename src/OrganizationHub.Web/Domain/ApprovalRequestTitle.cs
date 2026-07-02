namespace OrganizationHub.Web.Domain;

public readonly record struct ApprovalRequestTitle
{
    public const int MAXIMUM_LENGTH = 180;

    private const string DEFAULT_TITLE = "결재 요청";

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

    public static ApprovalRequestTitle Empty
    {
        get
        {
            return CreateOptional(string.Empty);
        }
    }

    public static ApprovalRequestTitle CreateOptional(string? titleOrNull)
    {
        return new ApprovalRequestTitle(titleOrNull);
    }

    private ApprovalRequestTitle(string? titleOrNull)
    {
        Value = normalizeTitle(titleOrNull);
    }

    public string GetValueOrFallback(DriveItemName fallbackTitle)
    {
        if (HasValue)
        {
            return Value;
        }

        if (fallbackTitle.HasValue == false)
        {
            return DEFAULT_TITLE;
        }

        return normalizeTitle(fallbackTitle.GetRequiredValue());
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }

    private static string normalizeTitle(string? titleOrNull)
    {
        if (string.IsNullOrWhiteSpace(titleOrNull))
        {
            return string.Empty;
        }

        string normalizedTitle = titleOrNull.Trim();
        if (normalizedTitle.Length <= MAXIMUM_LENGTH)
        {
            return normalizedTitle;
        }

        return normalizedTitle.Substring(0, MAXIMUM_LENGTH);
    }
}
