namespace OrganizationHub.Web.Domain;

public readonly record struct DriveFolderID
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

    public DriveFolderID(string value)
    {
        Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public override string ToString()
    {
        return Value;
    }
}
