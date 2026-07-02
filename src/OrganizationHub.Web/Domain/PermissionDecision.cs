namespace OrganizationHub.Web.Domain;

public sealed class PermissionDecision
{
    public bool IsAllowed
    {
        get; private init;
    }
    public string Reason { get; private init; } = string.Empty;
    public string ScopeLabel { get; private init; } = string.Empty;

    private PermissionDecision(
        EPermissionDecisionState decisionState,
        PermissionDecisionReason reason,
        PermissionScopeLabel scopeLabel)
    {
        IsAllowed = decisionState == EPermissionDecisionState.Allowed;
        Reason = reason.GetOptionalValue();
        ScopeLabel = scopeLabel.GetRequiredValue();
    }

    public static PermissionDecision Allow(PermissionScopeLabel scopeLabel)
    {
        return new PermissionDecision(EPermissionDecisionState.Allowed, PermissionDecisionReason.Empty, scopeLabel);
    }

    public static PermissionDecision Deny(PermissionDecisionReason reason, PermissionScopeLabel scopeLabel)
    {
        return new PermissionDecision(EPermissionDecisionState.Denied, reason, scopeLabel);
    }
}
