using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureApprovalWorkflows(ModelBuilder modelBuilder)
    {
        configureApprovalWorkflowHeaders(modelBuilder);
        configureApprovalWorkflowSteps(modelBuilder);
    }

    private static void configureApprovalWorkflowHeaders(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<ApprovalWorkflow> approvalWorkflowBuilder = modelBuilder.Entity<ApprovalWorkflow>();
        approvalWorkflowBuilder.HasKey(approvalWorkflow => approvalWorkflow.Id);
        approvalWorkflowBuilder.HasIndex(approvalWorkflow => new { approvalWorkflow.AppliesToTeamId, approvalWorkflow.AppliesToPositionId, approvalWorkflow.Priority });
        approvalWorkflowBuilder.Property(approvalWorkflow => approvalWorkflow.Name).HasMaxLength(120).IsRequired();
        approvalWorkflowBuilder.Property(approvalWorkflow => approvalWorkflow.Description).HasMaxLength(500).IsRequired();
        approvalWorkflowBuilder
            .HasOne(approvalWorkflow => approvalWorkflow.AppliesToTeam)
            .WithMany(team => team.ApprovalWorkflows)
            .HasForeignKey(approvalWorkflow => approvalWorkflow.AppliesToTeamId)
            .OnDelete(DeleteBehavior.Restrict);
        approvalWorkflowBuilder
            .HasOne(approvalWorkflow => approvalWorkflow.AppliesToPosition)
            .WithMany(position => position.RequesterApprovalWorkflows)
            .HasForeignKey(approvalWorkflow => approvalWorkflow.AppliesToPositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void configureApprovalWorkflowSteps(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<ApprovalWorkflowStep> approvalWorkflowStepBuilder = modelBuilder.Entity<ApprovalWorkflowStep>();
        approvalWorkflowStepBuilder.HasKey(approvalWorkflowStep => approvalWorkflowStep.Id);
        approvalWorkflowStepBuilder
            .HasIndex(approvalWorkflowStep => new { approvalWorkflowStep.ApprovalWorkflowId, approvalWorkflowStep.StepOrder })
            .IsUnique();
        approvalWorkflowStepBuilder.Property(approvalWorkflowStep => approvalWorkflowStep.StepName).HasMaxLength(120).IsRequired();
        approvalWorkflowStepBuilder
            .Property(approvalWorkflowStep => approvalWorkflowStep.ApproverSelectionType)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();
        approvalWorkflowStepBuilder.Property(approvalWorkflowStep => approvalWorkflowStep.RequiredPermissionKey).HasMaxLength(120).IsRequired();
        approvalWorkflowStepBuilder
            .Property(approvalWorkflowStep => approvalWorkflowStep.ScopeType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        approvalWorkflowStepBuilder
            .HasOne(approvalWorkflowStep => approvalWorkflowStep.ApprovalWorkflow)
            .WithMany(approvalWorkflow => approvalWorkflow.Steps)
            .HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApprovalWorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        approvalWorkflowStepBuilder
            .HasOne(approvalWorkflowStep => approvalWorkflowStep.ApproverUser)
            .WithMany()
            .HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);
        approvalWorkflowStepBuilder
            .HasOne(approvalWorkflowStep => approvalWorkflowStep.ApproverPosition)
            .WithMany(position => position.ApprovalWorkflowSteps)
            .HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApproverPositionId)
            .OnDelete(DeleteBehavior.Restrict);
        approvalWorkflowStepBuilder
            .HasOne(approvalWorkflowStep => approvalWorkflowStep.ApproverTeam)
            .WithMany()
            .HasForeignKey(approvalWorkflowStep => approvalWorkflowStep.ApproverTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
