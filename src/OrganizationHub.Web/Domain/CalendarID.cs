namespace OrganizationHub.Web.Domain;

public readonly record struct CalendarID
{
    public string Value
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

    public CalendarID(string value)
    {
        Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public override string ToString()
    {
        return Value;
    }
}
