using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDbContext
{
    private void prepareTeamAndWorkflowRevisions()
    {
        int[] workflowIds = ChangeTracker.Entries<ApprovalWorkflowStep>().Where(entry => entry.State != EntityState.Unchanged && entry.State != EntityState.Detached)
            .Select(entry => entry.Entity.ApprovalWorkflowId).Where(id => id > 0).Distinct().ToArray();
        foreach (int workflowId in workflowIds)
        {
            ApprovalWorkflow? workflowOrNull = ApprovalWorkflows.Find(workflowId);
            if (workflowOrNull != null && Entry(workflowOrNull).State != EntityState.Added && Entry(workflowOrNull).State != EntityState.Deleted)
            {
                Entry(workflowOrNull).Property(workflow => workflow.Revision).IsModified = true;
            }
        }
        foreach (EntityEntry<Team> entry in ChangeTracker.Entries<Team>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Revision = entry.Property(team => team.Revision).OriginalValue + 1;
            }
        }
        foreach (EntityEntry<ApprovalWorkflow> entry in ChangeTracker.Entries<ApprovalWorkflow>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Revision = entry.Property(workflow => workflow.Revision).OriginalValue + 1;
            }
        }
    }
}
