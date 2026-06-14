namespace OrganizationHub.Web.Domain;

public readonly record struct TodoTaskID(string Value)
{
    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public static TodoTaskID Empty
    {
        get
        {
            return new TodoTaskID(string.Empty);
        }
    }
}
