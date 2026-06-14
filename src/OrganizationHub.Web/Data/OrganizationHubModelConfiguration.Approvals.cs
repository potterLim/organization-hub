using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureApprovals(ModelBuilder modelBuilder)
    {
        configureApprovalRequests(modelBuilder);
        configureApprovalSteps(modelBuilder);
        configureApprovalAuditLogs(modelBuilder);
    }

    private static void configureApprovalRequests(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<ApprovalRequest> approvalRequestBuilder = modelBuilder.Entity<ApprovalRequest>();
        approvalRequestBuilder.HasKey(request => request.Id);
        approvalRequestBuilder.HasIndex(request => request.FileId);
        approvalRequestBuilder.HasIndex(request => request.ApprovedFileId);
        approvalRequestBuilder.HasIndex(request => request.TeamId);
        approvalRequestBuilder.HasIndex(request => request.RequesterUserId);
        approvalRequestBuilder.HasIndex(request => request.Status);
        approvalRequestBuilder.Property(request => request.Title).HasMaxLength(180).IsRequired();
        approvalRequestBuilder.Property(request => request.FileId).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.FileName).HasMaxLength(512).IsRequired();
        approvalRequestBuilder.Property(request => request.MimeType).HasMaxLength(160).IsRequired();
        approvalRequestBuilder.Property(request => request.SourceFolderId).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.SourceFolderKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalRequestBuilder.Property(request => request.TeamName).HasMaxLength(120).IsRequired();
        approvalRequestBuilder.Property(request => request.RequesterDisplayName).HasMaxLength(120).IsRequired();
        approvalRequestBuilder.Property(request => request.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalRequestBuilder.Property(request => request.ApprovedFileId).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.ApprovedFolderId).HasMaxLength(256).IsRequired();
        approvalRequestBuilder.Property(request => request.ApprovedFolderDisplayName).HasMaxLength(256).IsRequired();
        approvalRequestBuilder
            .HasOne(request => request.Team)
            .WithMany(team => team.ApprovalRequests)
            .HasForeignKey(request => request.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        approvalRequestBuilder
            .HasOne(request => request.RequesterUser)
            .WithMany()
            .HasForeignKey(request => request.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void configureApprovalSteps(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<ApprovalStep> approvalStepBuilder = modelBuilder.Entity<ApprovalStep>();
        approvalStepBuilder.HasKey(step => step.Id);
        approvalStepBuilder.HasIndex(step => new { step.ApprovalRequestId, step.StepNumber }).IsUnique();
        approvalStepBuilder.HasIndex(step => step.ApproverUserId);
        approvalStepBuilder.HasIndex(step => step.Status);
        approvalStepBuilder.Property(step => step.ApproverDisplayName).HasMaxLength(120).IsRequired();
        approvalStepBuilder.Property(step => step.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalStepBuilder.Property(step => step.Comment).HasMaxLength(1000).IsRequired();
        approvalStepBuilder
            .HasOne(step => step.ApprovalRequest)
            .WithMany(request => request.Steps)
            .HasForeignKey(step => step.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        approvalStepBuilder
            .HasOne(step => step.ApproverUser)
            .WithMany()
            .HasForeignKey(step => step.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void configureApprovalAuditLogs(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<ApprovalAuditLog> approvalAuditBuilder = modelBuilder.Entity<ApprovalAuditLog>();
        approvalAuditBuilder.HasKey(auditLog => auditLog.Id);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.ApprovalRequestId);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.ActorUserId);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.Action);
        approvalAuditBuilder.HasIndex(auditLog => auditLog.CreatedAt);
        approvalAuditBuilder.Property(auditLog => auditLog.ActorDisplayName).HasMaxLength(120).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.Comment).HasMaxLength(1000).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.PreviousApproverDisplayName).HasMaxLength(120).IsRequired();
        approvalAuditBuilder.Property(auditLog => auditLog.NewApproverDisplayName).HasMaxLength(120).IsRequired();
        approvalAuditBuilder
            .HasOne(auditLog => auditLog.ApprovalRequest)
            .WithMany(request => request.AuditLogs)
            .HasForeignKey(auditLog => auditLog.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
