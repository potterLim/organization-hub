using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class SessionAccessResult
{
    public required ESessionAccessState State
    {
        get; init;
    }
    public required string Fingerprint
    {
        get; init;
    }
}
