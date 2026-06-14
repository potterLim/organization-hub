namespace OrganizationHub.Web.Domain;

public readonly record struct DriveItemID(string Value)
{
    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static DriveItemID Empty
    {
        get
        {
            return new DriveItemID(string.Empty);
        }
    }
}
