using System;

namespace OrganizationHub.Web.Data.Entities;

public sealed class GoogleStorageConnection
{
    public int Id
    {
        get; set;
    }
    public string ConnectedEmail { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public bool IsActive
    {
        get; set;
    }
    public DateTime ConnectedAt
    {
        get; set;
    }
    public DateTime? LastValidatedAt
    {
        get; set;
    }
    public DateTime UpdatedAt
    {
        get; set;
    }
}
