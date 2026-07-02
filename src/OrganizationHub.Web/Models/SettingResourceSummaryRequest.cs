using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class SettingResourceSummaryRequest
{
    public required ESettingResourceSummaryKind ResourceKind
    {
        get; init;
    }

    public required string GroupName
    {
        get; init;
    }

    public required string DisplayName
    {
        get; init;
    }

    public required string ResourceId
    {
        get; init;
    }
}
