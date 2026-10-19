using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class UserSessionCreateRequest
{
    public int AuthenticationVersion
    {
        get; init;
    }
    public required Username Username
    {
        get; init;
    }

    public required DisplayName DisplayName
    {
        get; init;
    }

    public required DisplayName TeamDisplayName
    {
        get; init;
    }

    public required UserAccountId UserAccountId
    {
        get; init;
    }

    public required EStoredCompatibilityRole StoredCompatibilityRole
    {
        get; init;
    }

    public required OrganizationTeamId? OrganizationTeamIdOrNull
    {
        get; init;
    }

    public required OrganizationTeamKey TeamKey
    {
        get; init;
    }
}
