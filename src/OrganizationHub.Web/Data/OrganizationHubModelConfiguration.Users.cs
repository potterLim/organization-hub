using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureUsers(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<UserAccount> userBuilder = modelBuilder.Entity<UserAccount>();
        userBuilder.HasKey(user => user.Id);
        userBuilder.HasIndex(user => user.Username).IsUnique();
        userBuilder.Property(user => user.Username).HasMaxLength(64).IsRequired();
        userBuilder.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
        userBuilder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
        userBuilder.Property(user => user.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        userBuilder
            .HasOne(user => user.Team)
            .WithMany(team => team.UserAccounts)
            .HasForeignKey(user => user.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        userBuilder
            .HasOne(user => user.Position)
            .WithMany(position => position.UserAccounts)
            .HasForeignKey(user => user.PositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
