namespace OrganizationHub.Web.Domain;

public sealed class PermissionDecision
{
    public bool IsAllowed
    {
        get; private init;
    }
    public string Reason { get; private init; } = string.Empty;
    public string ScopeLabel { get; private init; } = string.Empty;

    private PermissionDecision(EPermissionDecisionState decisionState, string reason, string scopeLabel)
    {
        IsAllowed = decisionState == EPermissionDecisionState.Allowed;
        Reason = reason;
        ScopeLabel = scopeLabel;
    }

    public static PermissionDecision Allow(string scopeLabel)
    {
        return new PermissionDecision(EPermissionDecisionState.Allowed, string.Empty, scopeLabel);
    }

    public static PermissionDecision Allow(string scopeLabel, string reason)
    {
        return new PermissionDecision(EPermissionDecisionState.Allowed, reason, scopeLabel);
    }

    public static PermissionDecision Deny(string reason, string scopeLabel)
    {
        return new PermissionDecision(EPermissionDecisionState.Denied, reason, scopeLabel);
    }
}
