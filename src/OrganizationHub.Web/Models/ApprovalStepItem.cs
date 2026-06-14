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
}
