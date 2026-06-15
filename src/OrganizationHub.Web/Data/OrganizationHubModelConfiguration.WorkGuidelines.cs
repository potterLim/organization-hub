using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureWorkGuidelines(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<WorkGuidelineSection> sectionBuilder = modelBuilder.Entity<WorkGuidelineSection>();
        sectionBuilder.HasKey(section => section.Id);
        sectionBuilder.HasIndex(section => section.SortOrder);
        sectionBuilder.Property(section => section.Title).HasMaxLength(120).IsRequired();
        sectionBuilder.Property(section => section.Body).HasMaxLength(4000).IsRequired();
    }
}
