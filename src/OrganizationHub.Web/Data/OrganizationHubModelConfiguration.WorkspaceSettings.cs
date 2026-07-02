using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureWorkspaceSettings(ModelBuilder modelBuilder)
    {
        configureGlobalSettings(modelBuilder);
        configureGoogleStorageConnections(modelBuilder);
    }

    private static void configureGlobalSettings(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<GlobalSetting> settingBuilder = modelBuilder.Entity<GlobalSetting>();
        settingBuilder.HasKey(setting => setting.Id);
        settingBuilder.Property(setting => setting.OfficialRootFolderId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.PublicFolderId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.CommonFolderId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ResponsibleResourcesFolderId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.WorkResourcesFolderId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ArchiveFolderId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.OrganizationCalendarId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ResponsibleCalendarId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.OrganizationTaskListId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ResponsibleTaskListId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.NoticeFileId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.WorkGuidelineFileId).HasMaxLength(256);
        settingBuilder.Property(setting => setting.ShouldShowTodoAuthor).HasColumnName("ShowTodoAuthor").HasDefaultValue(true);
        settingBuilder.Property(setting => setting.ShouldShowCalendarAuthor).HasColumnName("ShowCalendarAuthor").HasDefaultValue(true);
    }

    private static void configureGoogleStorageConnections(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<GoogleStorageConnection> googleConnectionBuilder = modelBuilder.Entity<GoogleStorageConnection>();
        googleConnectionBuilder.HasKey(connection => connection.Id);
        googleConnectionBuilder.Property(connection => connection.ConnectedEmail).HasMaxLength(320).IsRequired();
        googleConnectionBuilder.Property(connection => connection.EncryptedRefreshToken).IsRequired();
        googleConnectionBuilder.Property(connection => connection.Scopes).HasMaxLength(1024).IsRequired();
    }
}
