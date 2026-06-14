using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data.Entities;

public class ApprovalStep
{
    public int Id
    {
        get; set;
    }
    public int ApprovalRequestId
    {
        get; set;
    }
    public ApprovalRequest? ApprovalRequest
    {
        get; set;
    }
    public int StepNumber
    {
        get; set;
    }
    public int ApproverUserId
    {
        get; set;
    }
    public UserAccount? ApproverUser
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
