using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureAuditLogs(ModelBuilder modelBuilder)
    {
        configureUploadAuditLogs(modelBuilder);
        configureCalendarEventDeletionAuditLogs(modelBuilder);
    }

    private static void configureUploadAuditLogs(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<UploadAuditLog> uploadAuditBuilder = modelBuilder.Entity<UploadAuditLog>();
        uploadAuditBuilder.HasKey(auditLog => auditLog.Id);
        uploadAuditBuilder.HasIndex(auditLog => auditLog.FileId);
        uploadAuditBuilder.HasIndex(auditLog => new { auditLog.FileId, auditLog.UploadedAt });
        uploadAuditBuilder.HasIndex(auditLog => auditLog.FolderId);
        uploadAuditBuilder.HasIndex(auditLog => new { auditLog.FolderId, auditLog.UploadedAt });
        uploadAuditBuilder.Property(auditLog => auditLog.FileId).HasMaxLength(256).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FileName).HasMaxLength(512).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FolderId).HasMaxLength(256).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FolderDisplayName).HasMaxLength(160).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.FolderKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.UploaderDisplayName).HasMaxLength(120).IsRequired();
        uploadAuditBuilder
            .Property(auditLog => auditLog.UploaderStoredCompatibilityRole)
            .HasColumnName("UploaderRole")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.UploaderTeamName).HasMaxLength(120).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.UploaderPositionName).HasMaxLength(120).IsRequired();
        uploadAuditBuilder.Property(auditLog => auditLog.TeamName).HasMaxLength(120).IsRequired();
    }

    private static void configureCalendarEventDeletionAuditLogs(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<CalendarEventDeletionAuditLog> calendarDeletionAuditBuilder = modelBuilder.Entity<CalendarEventDeletionAuditLog>();
        calendarDeletionAuditBuilder.HasKey(auditLog => auditLog.Id);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.CalendarId);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.EventId);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.DeletedAt);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.DeletedByUserId);
        calendarDeletionAuditBuilder.HasIndex(auditLog => auditLog.Status);
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.CalendarId).HasMaxLength(256).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.CalendarName).HasMaxLength(160).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.EventId).HasMaxLength(256).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.EventSummary).HasMaxLength(512).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.EventLocation).HasMaxLength(256).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.DeletedByDisplayName).HasMaxLength(120).IsRequired();
        calendarDeletionAuditBuilder
            .Property(auditLog => auditLog.DeletedByStoredCompatibilityRole)
            .HasColumnName("DeletedByRole")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.DeletedByPositionName).HasMaxLength(120).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.DeletedByTeamName).HasMaxLength(120).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        calendarDeletionAuditBuilder.Property(auditLog => auditLog.ErrorMessage).HasMaxLength(1024).IsRequired();
    }
}
