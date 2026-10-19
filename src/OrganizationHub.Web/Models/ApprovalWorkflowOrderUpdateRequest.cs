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

    public IReadOnlyDictionary<int, int> ExpectedRevisions
    {
        get;
    }

    public ApprovalWorkflowOrderUpdateRequest(IReadOnlyList<ApprovalWorkflowId> orderedWorkflowIds, IReadOnlyDictionary<int, int>? expectedRevisionsOrNull = null)
    {
        ArgumentNullException.ThrowIfNull(orderedWorkflowIds);

        OrderedWorkflowIds = orderedWorkflowIds;
        ExpectedRevisions = expectedRevisionsOrNull == null ? new Dictionary<int, int>() : new Dictionary<int, int>(expectedRevisionsOrNull);
    }
}
