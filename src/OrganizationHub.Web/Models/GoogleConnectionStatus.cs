using System;

namespace OrganizationHub.Web.Models;

public sealed class GoogleConnectionStatus
{
    public bool HasOAuthConfiguration
    {
        get; set;
    }
    public bool IsConnected
    {
        get; set;
    }
    public string ConnectedEmail { get; set; } = string.Empty;
    public DateTime? ConnectedAt
    {
        get; set;
    }
    public DateTime? LastValidatedAt
    {
        get; set;
    }
    public string Message { get; set; } = string.Empty;
}
