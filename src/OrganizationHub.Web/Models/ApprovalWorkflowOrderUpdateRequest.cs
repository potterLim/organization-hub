using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ApprovalWorkflowOrderUpdateRequest
{
    public IReadOnlyList<ApprovalWorkflowId> OrderedWorkflowIds
    {
        get;
    }

    public ApprovalWorkflowOrderUpdateRequest(IReadOnlyList<ApprovalWorkflowId> orderedWorkflowIds)
    {
        OrderedWorkflowIds = orderedWorkflowIds;
    }
}
