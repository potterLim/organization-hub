using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalStepItem
{
    public int StepNumber
    {
        get; set;
    }
    public int ApproverUserId
    {
        get; set;
    }
    public string ApproverTeamName { get; set; } = string.Empty;
    public string ApproverPositionName { get; set; } = string.Empty;
    public string ApproverDisplayName { get; set; } = string.Empty;
    public EApprovalStepStatus Status
    {
        get; set;
    }
    public DateTime? ActionedAt
    {
        get; set;
    }
    public string Comment { get; set; } = string.Empty;

    public string GetApproverAffiliationText()
    {
        OrganizationAffiliationLabel affiliationLabel = new OrganizationAffiliationLabel(ApproverTeamName, ApproverPositionName);
        string affiliationText = affiliationLabel.GetFullText();
        if (string.IsNullOrWhiteSpace(affiliationText))
        {
            return ApproverDisplayName;
        }

        return affiliationText;
    }
}
