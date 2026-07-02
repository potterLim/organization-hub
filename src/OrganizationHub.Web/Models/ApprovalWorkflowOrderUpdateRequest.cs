using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class ApprovalWorkflowOrderUpdateRequest
{
    public IReadOnlyList<ApprovalWorkflowId> OrderedWorkflowIds
    {
        get;
    }

    public ApprovalWorkflowOrderUpdateRequest(IReadOnlyList<ApprovalWorkflowId> orderedWorkflowIds)
    {
        ArgumentNullException.ThrowIfNull(orderedWorkflowIds);

        OrderedWorkflowIds = orderedWorkflowIds;
    }
}
