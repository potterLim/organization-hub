namespace OrganizationHub.Web.Domain;

public readonly record struct SearchKeyword
{
    private readonly string? mValueOrNull;

    public string Value
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

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static SearchKeyword Empty
    {
        get
        {
            return new SearchKeyword(string.Empty);
        }
    }

    private SearchKeyword(string keyword)
    {
        mValueOrNull = keyword;
    }

    public static SearchKeyword CreateOptional(string? keywordOrNull)
    {
        if (string.IsNullOrWhiteSpace(keywordOrNull))
        {
            return Empty;
        }

        return new SearchKeyword(keywordOrNull.Trim());
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
