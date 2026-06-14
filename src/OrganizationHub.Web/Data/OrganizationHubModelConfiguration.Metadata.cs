using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureMetadata(ModelBuilder modelBuilder)
    {
        configureTodoTaskMetadata(modelBuilder);
        configureCalendarEventMetadata(modelBuilder);
    }

    private static void configureTodoTaskMetadata(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<TodoTaskMetadata> todoTaskMetadataBuilder = modelBuilder.Entity<TodoTaskMetadata>();
        todoTaskMetadataBuilder.HasKey(metadata => metadata.Id);
        todoTaskMetadataBuilder.HasIndex(metadata => new { metadata.TaskListId, metadata.TaskId }).IsUnique();
        todoTaskMetadataBuilder.HasIndex(metadata => metadata.CreatedByUserId);
        todoTaskMetadataBuilder.Property(metadata => metadata.TaskListId).HasMaxLength(256).IsRequired();
        todoTaskMetadataBuilder.Property(metadata => metadata.TaskId).HasMaxLength(256).IsRequired();
        todoTaskMetadataBuilder.Property(metadata => metadata.CreatedByDisplayName).HasMaxLength(120).IsRequired();
    }

    private static void configureCalendarEventMetadata(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<CalendarEventMetadata> calendarEventMetadataBuilder = modelBuilder.Entity<CalendarEventMetadata>();
        calendarEventMetadataBuilder.HasKey(metadata => metadata.Id);
        calendarEventMetadataBuilder.HasIndex(metadata => new { metadata.CalendarId, metadata.EventId }).IsUnique();
        calendarEventMetadataBuilder.HasIndex(metadata => metadata.CreatedByUserId);
        calendarEventMetadataBuilder.Property(metadata => metadata.CalendarId).HasMaxLength(256).IsRequired();
        calendarEventMetadataBuilder.Property(metadata => metadata.EventId).HasMaxLength(256).IsRequired();
        calendarEventMetadataBuilder.Property(metadata => metadata.CreatedByDisplayName).HasMaxLength(120).IsRequired();
    }
}
