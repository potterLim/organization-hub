namespace OrganizationHub.Web.Data;

internal sealed class WorkGuidelineSeedSection
{
    public required string Title
    {
        get; init;
    }

    public required string Body
    {
        get; init;
    }

    public required int SortOrder
    {
        get; init;
    }
}
