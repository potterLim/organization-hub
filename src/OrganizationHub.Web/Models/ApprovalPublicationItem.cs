using System;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalPublicationItem
{
    public required int RequestId
    {
        get; init;
    }
    public required string Title
    {
        get; init;
    }
    public required int AttemptCount
    {
        get; init;
    }
    public required DateTime? LastAttemptAt
    {
        get; init;
    }
    public required DateTime NextAttemptAt
    {
        get; init;
    }
    public required bool IsProcessing
    {
        get; init;
    }
    public required string LastError
    {
        get; init;
    }
}
