namespace OrganizationHub.Web.Domain;

public readonly record struct NavigationPath
{
    public const string DASHBOARD_VALUE = "/dashboard";

    private readonly string? mValueOrNull;

    private string Value
    {
        get
        {
            if (mValueOrNull == null)
            {
                return DASHBOARD_VALUE;
            }

            return mValueOrNull;
        }
    }

    public static NavigationPath Dashboard
    {
        get
        {
            return new NavigationPath(DASHBOARD_VALUE);
        }
    }

    private NavigationPath(string path)
    {
        mValueOrNull = path;
    }

    public static NavigationPath CreateOptional(string? pathOrNull)
    {
        if (string.IsNullOrWhiteSpace(pathOrNull) || pathOrNull == "/")
        {
            return Dashboard;
        }

        string normalizedPath = pathOrNull.Trim();
        if (normalizedPath.EndsWith("/") && normalizedPath.Length > 1)
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        return new NavigationPath(normalizedPath);
    }

    public string GetRequiredValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
