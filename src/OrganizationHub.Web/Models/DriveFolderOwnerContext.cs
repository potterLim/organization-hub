using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class DriveFolderOwnerContext
{
    public OrganizationTeamId? TeamIdOrNull
    {
        get; private init;
    }

    public OrganizationTeamKey TeamKey
    {
        get; private init;
    }

    public string TeamName
    {
        get; private init;
    } = string.Empty;

    public static DriveFolderOwnerContext Empty
    {
        get
        {
            return new DriveFolderOwnerContext(null, OrganizationTeamKey.Empty, string.Empty);
        }
    }

    public DriveFolderOwnerContext(OrganizationTeamId? teamIdOrNull, OrganizationTeamKey teamKey, string teamName)
    {
        TeamIdOrNull = teamIdOrNull;
        TeamKey = teamKey;
        TeamName = teamName;
    }
}
