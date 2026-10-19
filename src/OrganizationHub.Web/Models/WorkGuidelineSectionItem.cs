namespace OrganizationHub.Web.Models;

public sealed class WorkGuidelineSectionItem
{
    public required int Id
    {
        get; init;
    }
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
