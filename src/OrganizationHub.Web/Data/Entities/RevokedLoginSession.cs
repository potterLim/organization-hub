using System;

namespace OrganizationHub.Web.Data.Entities;

public sealed class RevokedLoginSession
{
    public string SessionId { get; set; } = string.Empty;
    public int UserAccountId
    {
        get; set;
    }
    public DateTime RevokedAtUtc
    {
        get; set;
    }
}
