namespace OrganizationHub.Web.Data;

internal sealed class TeamSeedDefinition
{
    public required string TeamKey
    {
        get; init;
    }

    public required string DisplayName
    {
        get; init;
    }

    public required int SortOrder
    {
        get; init;
    }
}
