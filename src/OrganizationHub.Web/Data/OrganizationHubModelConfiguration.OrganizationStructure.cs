using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureOrganizationStructure(ModelBuilder modelBuilder)
    {
        configureTeams(modelBuilder);
        configurePositions(modelBuilder);
        configurePositionPermissionGrants(modelBuilder);
    }

    private static void configureTeams(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<Team> teamBuilder = modelBuilder.Entity<Team>();
        teamBuilder.HasKey(team => team.Id);
        teamBuilder.Property(team => team.Revision).IsConcurrencyToken();
        teamBuilder.HasIndex(team => team.TeamKey).IsUnique();
        teamBuilder.Property(team => team.TeamKey).HasMaxLength(64).IsRequired();
        teamBuilder.Property(team => team.DisplayName).HasMaxLength(120).IsRequired();
        teamBuilder.Property(team => team.MaterialsFolderId).HasMaxLength(256);
        teamBuilder.Property(team => team.ApprovedFolderId).HasMaxLength(256);
        teamBuilder.Property(team => team.CalendarId).HasMaxLength(256);
        teamBuilder.Property(team => team.TaskListId).HasMaxLength(256);
    }

    private static void configurePositions(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<OrganizationPosition> positionBuilder = modelBuilder.Entity<OrganizationPosition>();
        positionBuilder.HasKey(position => position.Id);
        positionBuilder.Property(position => position.Revision).IsConcurrencyToken();
        positionBuilder.HasIndex(position => new { position.TeamId, position.Name }).IsUnique();
        positionBuilder.Property(position => position.Name).HasMaxLength(120).IsRequired();
        positionBuilder.Property(position => position.Description).HasMaxLength(500).IsRequired();
        positionBuilder
            .HasOne(position => position.Team)
            .WithMany(team => team.Positions)
            .HasForeignKey(position => position.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void configurePositionPermissionGrants(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<PositionPermissionGrant> permissionGrantBuilder = modelBuilder.Entity<PositionPermissionGrant>();
        permissionGrantBuilder.HasKey(permissionGrant => permissionGrant.Id);
        permissionGrantBuilder
            .HasIndex(permissionGrant => new
            {
                permissionGrant.PositionId,
                permissionGrant.PermissionKey,
                permissionGrant.ScopeType,
                permissionGrant.TargetTeamId
            })
            .IsUnique();
        permissionGrantBuilder.Property(permissionGrant => permissionGrant.PermissionKey).HasMaxLength(120).IsRequired();
        permissionGrantBuilder.Property(permissionGrant => permissionGrant.ScopeType).HasConversion<string>().HasMaxLength(32).IsRequired();
        permissionGrantBuilder
            .HasOne(permissionGrant => permissionGrant.Position)
            .WithMany(position => position.PermissionGrants)
            .HasForeignKey(permissionGrant => permissionGrant.PositionId)
            .OnDelete(DeleteBehavior.Cascade);
        permissionGrantBuilder
            .HasOne(permissionGrant => permissionGrant.TargetTeam)
            .WithMany()
            .HasForeignKey(permissionGrant => permissionGrant.TargetTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
